using System.Collections;
using FON.Native;

namespace FON.Types;


/// <summary>
/// Key-value collection that holds primitives, strings, <see cref="RawData"/>, nested
/// collections and lists of each. Backed by a native collection.
/// </summary>
/// <remarks>
/// An owned collection (created directly, by <see cref="Clone"/>, or by deserializing a single
/// record) owns native memory; free it with <see cref="Dispose"/> (the finalizer is a safety net).
/// A collection read from a <see cref="FonDump"/> or from a nested getter is a view: it owns
/// nothing and keeps its root alive. A view throws <see cref="FonStaleViewException"/> once the
/// data it refers to was changed or removed, and <see cref="ObjectDisposedException"/> once its
/// root was disposed.
/// <para>
/// Putting an owned collection into another collection or dump transfers it: the instance passed
/// in becomes unusable and its views go stale. A view cannot be put into a container; call
/// <see cref="Clone"/> first.
/// </para>
/// </remarks>
public sealed class FonCollection : IEnumerable<KeyValuePair<string, object>>, IDisposable {
    private const long EmptyCollectionBytesEstimate = 64;

    private readonly FonCollectionHandle? ownedHandle;
    private readonly FonCollectionView? view;
    private long version;
    private long treeVersion;
    private bool transferred;


    public FonCollection() : this(CreateOwnHandle()) {
    }


    internal FonCollection(FonCollectionHandle ownedHandle) {
        this.ownedHandle = ownedHandle;
    }


    internal FonCollection(FonCollectionView view) {
        this.view = view;
    }


    public object this[string key] {
        get {
            int kind = KindCode(key);
            if (kind < 0) {
                throw new KeyNotFoundException($"Key '{key}' not found in FonCollection");
            }
            return GetBoxedValue(key, kind);
        }
        set {
            Set(key, value);
        }
    }


    /// <summary>Number of keys in this collection.</summary>
    public int Count => Read(raw => checked((int)NativeBindings.fon_collection_size(raw)));


    /// <summary>All keys in insertion order.</summary>
    public IReadOnlyList<string> Keys {
        get {
            List<KeyEntry> entries = Read(raw => NativeApi.ReadKeys(raw));
            List<string> keys = new List<string>(entries.Count);
            foreach (KeyEntry entry in entries) {
                keys.Add(entry.Key);
            }
            return keys;
        }
    }


    public void Dispose() {
        ownedHandle?.Dispose();
    }


    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
        foreach (KeyEntry entry in Read(raw => NativeApi.ReadKeys(raw))) {
            yield return new KeyValuePair<string, object>(entry.Key, GetBoxedValue(entry.Key, entry.Kind));
        }
    }


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    /// <summary>Adds a value under a new key. Throws <see cref="InvalidOperationException"/> if the key exists.</summary>
    public void Add(string key, object value) {
        PutValue(key, value, FonPutMode.Add);
    }


    /// <summary>Adds a value under a new key; returns false if the key exists.</summary>
    public bool TryAdd(string key, object value) {
        if (ContainsKey(key)) {
            return false;
        }
        PutValue(key, value, FonPutMode.Add);
        return true;
    }


    /// <summary>Stores a value under the key, replacing any existing value (and its type).</summary>
    public void Set(string key, object value) {
        PutValue(key, value, FonPutMode.Upsert);
    }


    /// <summary>Removes the key. Returns false if it was absent.</summary>
    public bool Remove(string key) {
        if (!ContainsKey(key)) {
            return false;
        }
        byte[] keyBytes = NativeApi.Encode(key);
        Mutate(raw => NativeApi.RemoveKey(raw, keyBytes));
        return true;
    }


    public bool ContainsKey(string key) {
        return KindCode(key) >= 0;
    }


    /// <summary>Kind of the value under the key, or null if the key is absent.</summary>
    public FonKind? KindOf(string key) {
        int kind = KindCode(key);
        if (kind < 0) {
            return null;
        }
        return new FonKind(FonKindCodes.ToType((char)(kind & 0xFF)), (kind & FonTypeCode.ArrayFlag) != 0);
    }


    /// <summary>Deep copy of this collection as a new owned collection.</summary>
    public FonCollection Clone() {
        IntPtr clone = Read(raw => NativeApi.CloneCollection(raw));
        return FromOwnedHandle(clone, EmptyCollectionBytesEstimate);
    }


    public object Get(string key) => this[key];


    public T Get<T>(string key) => (T)this[key];


    public object? TryGet(string key) {
        int kind = KindCode(key);
        return kind < 0 ? null : GetBoxedValue(key, kind);
    }


    public T? TryGet<T>(string key) where T : class => TryGet(key) as T;


    public T? TryGetNullable<T>(string key) where T : struct => TryGet(key) is T value ? value : null;




    public static FonCollection Serialize<T>(T obj) {
        System.Reflection.PropertyInfo[] properties = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        FonCollection collection = new FonCollection();

        foreach (System.Reflection.PropertyInfo property in properties) {
            object? value = property.GetValue(obj);
            if (value != null) {
                collection.Add(property.Name, value);
            }
        }

        return collection;
    }




    public T Deserialize<T>() where T : new() {
        T result = new T();
        object boxedResult = result;

        System.Reflection.PropertyInfo[] properties = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (System.Reflection.PropertyInfo property in properties) {
            object? value = TryGet(property.Name);
            if (value != null && property.PropertyType.IsInstanceOfType(value)) {
                property.SetValue(boxedResult, value);
            }
        }

        return (T)boxedResult;
    }



    // ==================== INTERNAL ====================

    /// <summary>
    /// Runs <paramref name="read"/> with the validated native handle of this collection. The
    /// collection is kept alive for the duration of the call.
    /// </summary>
    internal T Read<T>(Func<IntPtr, T> read) {
        IntPtr raw = Access();
        try {
            return read(raw);
        } finally {
            GC.KeepAlive(this);
        }
    }


    internal static FonCollection FromOwnedHandle(IntPtr raw, long estimatedBytes) {
        return new FonCollection(new FonCollectionHandle(raw, ownsHandle: true, estimatedBytes));
    }


    /// <summary>Validates an argument that is about to be stored in a container.</summary>
    internal static void ValidateOwnedRoot(FonCollection child) {
        ArgumentNullException.ThrowIfNull(child);
        if (child.view is not null) {
            throw new ArgumentException("A view cannot be stored in another collection or dump; call clone() first and store the copy.");
        }
        if (child.transferred || child.OwnedHandle.IsClosed) {
            throw new ObjectDisposedException(nameof(FonCollection), "The collection is disposed or was moved into another container.");
        }
    }


    /// <summary>Native pointer of an owned collection, for handing to a container.</summary>
    internal IntPtr OwnedPointer() {
        return OwnedHandle.DangerousGetHandle();
    }


    /// <summary>Marks this owned collection as moved into <paramref name="owner"/>.</summary>
    internal void TransferTo(FonNativeHandle owner) {
        transferred = true;
        owner.AdoptChild(OwnedHandle);
    }



    // ==================== VALIDATION ====================

    private FonCollectionHandle OwnedHandle => ownedHandle!;


    private static FonCollectionHandle CreateOwnHandle() {
        IntPtr raw = NativeBindings.fon_collection_create();
        return new FonCollectionHandle(raw, ownsHandle: true, EmptyCollectionBytesEstimate);
    }


    /// <summary>
    /// Returns the native pointer for this collection after validating it: disposed or
    /// transferred owned collections throw <see cref="ObjectDisposedException"/>; views check
    /// their root and their captured generations or versions.
    /// </summary>
    private IntPtr Access() {
        if (view is null) {
            if (transferred || OwnedHandle.IsClosed) {
                throw new ObjectDisposedException(nameof(FonCollection), "The collection is disposed or was moved into another container.");
            }
            return OwnedHandle.DangerousGetHandle();
        }
        return ResolveView(view);
    }


    private IntPtr ResolveView(FonCollectionView current) {
        if (current.dump is not null) {
            FonDump dump = current.dump;
            dump.Access();
            if (dump.version != current.capturedVersion) {
                if (!dump.TryGetGens(current.recordId, out uint recordGen, out uint treeGen)) {
                    throw NativeApi.StaleView();
                }
                if (recordGen != current.recordGen) {
                    throw NativeApi.StaleView();
                }
                if (current.isRecord) {
                    current.pointer = dump.GetRecordPointer(current.recordId);
                } else if (treeGen != current.treeGen) {
                    throw NativeApi.StaleView();
                }
                current.capturedVersion = dump.version;
            }
            return current.pointer;
        }

        FonCollection root = current.root!;
        root.EnsureRootAlive();
        if (root.version != current.capturedVersion) {
            if (root.treeVersion != current.treeVersion) {
                throw NativeApi.StaleView();
            }
            current.capturedVersion = root.version;
        }
        return current.pointer;
    }


    private void EnsureRootAlive() {
        if (transferred) {
            throw NativeApi.StaleView();
        }
        if (OwnedHandle.IsClosed) {
            throw new ObjectDisposedException(nameof(FonCollection));
        }
    }


    /// <summary>
    /// Checks the collections about to be stored in this one: owned, live, not this collection,
    /// not the root of a view of this collection, and not repeated.
    /// </summary>
    private void ValidateChildren(IReadOnlyList<FonCollection> children) {
        HashSet<FonCollection> seen = new HashSet<FonCollection>(ReferenceEqualityComparer.Instance);
        foreach (FonCollection child in children) {
            ValidateOwnedRoot(child);
            if (ReferenceEquals(child, this)) {
                throw new ArgumentException("A collection cannot contain itself.");
            }
            if (view is not null && ReferenceEquals(view.root, child)) {
                throw new ArgumentException("A collection cannot be stored inside its own nested view; that would create a cycle.");
            }
            if (!seen.Add(child)) {
                throw new ArgumentException("The same collection appears more than once.");
            }
        }
    }



    // ==================== MUTATION BOOKKEEPING ====================

    /// <summary>
    /// Runs a native mutation on this collection and then updates the versions that views
    /// validate against.
    /// </summary>
    private void Mutate(Action<IntPtr> change) {
        IntPtr raw = Access();
        try {
            change(raw);
        } finally {
            GC.KeepAlive(this);
        }
        AfterMutation();
    }


    private void AfterMutation() {
        if (view is null) {
            treeVersion++;
            version++;
            return;
        }

        if (view.dump is not null) {
            FonDump dump = view.dump;
            dump.Touch(view.recordId);
            dump.version++;
            if (view.isRecord) {
                view.pointer = dump.GetRecordPointer(view.recordId);
            } else {
                dump.TryGetGens(view.recordId, out view.recordGen, out view.treeGen);
            }
            view.capturedVersion = dump.version;
            return;
        }

        FonCollection root = view.root!;
        root.treeVersion++;
        root.version++;
        view.treeVersion = root.treeVersion;
        view.capturedVersion = root.version;
    }


    private FonNativeHandle OwnerHandle() {
        if (view is null) {
            return OwnedHandle;
        }
        if (view.dump is not null) {
            return view.dump.Handle;
        }
        return view.root!.OwnedHandle;
    }


    private void TransferChildren(IReadOnlyList<FonCollection> children) {
        FonNativeHandle owner = OwnerHandle();
        foreach (FonCollection child in children) {
            child.TransferTo(owner);
        }
    }


    /// <summary>
    /// Creates the view for a nested collection read from this one. The child's root is this
    /// collection's root (or the dump, for collections inside a record).
    /// </summary>
    private FonCollection CreateChildView(IntPtr childPointer) {
        return CreateChildViews(new IntPtr[] { childPointer })[0];
    }


    /// <summary>
    /// Creates the views for several nested collections read from this one in a single pass.
    /// The record generations of a dump-owned child are fetched once for the whole batch.
    /// </summary>
    private List<FonCollection> CreateChildViews(IReadOnlyList<IntPtr> childPointers) {
        List<FonCollection> children = new List<FonCollection>(childPointers.Count);
        if (childPointers.Count == 0) {
            return children;
        }

        if (view is null) {
            foreach (IntPtr childPointer in childPointers) {
                children.Add(new FonCollection(new FonCollectionView {
                    root = this,
                    treeVersion = treeVersion,
                    capturedVersion = version,
                    pointer = childPointer
                }));
            }
            return children;
        }

        if (view.dump is not null) {
            FonDump dump = view.dump;
            if (!dump.TryGetGens(view.recordId, out uint recordGen, out uint treeGen)) {
                throw NativeApi.StaleView();
            }
            foreach (IntPtr childPointer in childPointers) {
                children.Add(new FonCollection(new FonCollectionView {
                    dump = dump,
                    recordId = view.recordId,
                    recordGen = recordGen,
                    treeGen = treeGen,
                    capturedVersion = dump.version,
                    pointer = childPointer
                }));
            }
            return children;
        }

        FonCollection root = view.root!;
        foreach (IntPtr childPointer in childPointers) {
            children.Add(new FonCollection(new FonCollectionView {
                root = root,
                treeVersion = root.treeVersion,
                capturedVersion = root.version,
                pointer = childPointer
            }));
        }
        return children;
    }



    // ==================== KEYS AND VALUES ====================

    /// <summary>Packed kind of the key (see <see cref="FonTypeCode"/>), or -1 if absent.</summary>
    private int KindCode(string key) {
        byte[] keyBytes = NativeApi.Encode(key);
        return Read(raw => NativeApi.KindOf(raw, keyBytes));
    }


    private object GetBoxedValue(string key, int kind) {
        byte[] keyBytes = NativeApi.Encode(key);
        bool isArray = (kind & FonTypeCode.ArrayFlag) != 0;
        char typeChar = (char)(kind & 0xFF);
        return Read(raw => ReadValue(raw, keyBytes, typeChar, isArray));
    }


    private unsafe object ReadValue(IntPtr raw, byte[] keyBytes, char typeChar, bool isArray) {
        if (isArray) {
            return ReadArrayValue(raw, keyBytes, typeChar);
        }

        switch (typeChar) {
            case FonTypeCode.Byte:
                return NativeApi.GetScalar<byte>(raw, keyBytes, NativeBindings.fon_collection_get_byte);
            case FonTypeCode.Short:
                return NativeApi.GetScalar<short>(raw, keyBytes, NativeBindings.fon_collection_get_short);
            case FonTypeCode.Int:
                return NativeApi.GetScalar<int>(raw, keyBytes, NativeBindings.fon_collection_get_int);
            case FonTypeCode.UInt:
                return NativeApi.GetScalar<uint>(raw, keyBytes, NativeBindings.fon_collection_get_uint);
            case FonTypeCode.Long:
                return NativeApi.GetScalar<long>(raw, keyBytes, NativeBindings.fon_collection_get_long);
            case FonTypeCode.ULong:
                return NativeApi.GetScalar<ulong>(raw, keyBytes, NativeBindings.fon_collection_get_ulong);
            case FonTypeCode.Float:
                return NativeApi.GetScalar<float>(raw, keyBytes, NativeBindings.fon_collection_get_float);
            case FonTypeCode.Double:
                return NativeApi.GetScalar<double>(raw, keyBytes, NativeBindings.fon_collection_get_double);
            case FonTypeCode.Bool:
                return NativeApi.GetScalar<int>(raw, keyBytes, NativeBindings.fon_collection_get_bool) != 0;
            case FonTypeCode.String:
                return NativeApi.DecodeUtf8(NativeApi.GetBytes(raw, keyBytes, NativeBindings.fon_collection_get_string));
            case FonTypeCode.Raw:
                return new RawData(NativeApi.GetBytes(raw, keyBytes, NativeBindings.fon_collection_get_raw));
            case FonTypeCode.Object:
                return CreateChildView(NativeApi.GetChild(raw, keyBytes));
            default:
                throw new NotSupportedException($"Unknown native type tag '{typeChar}' (array=false)");
        }
    }


    private unsafe object ReadArrayValue(IntPtr raw, byte[] keyBytes, char typeChar) {
        switch (typeChar) {
            case FonTypeCode.Byte:
                return NativeApi.GetArray<byte>(raw, keyBytes, NativeBindings.fon_collection_get_byte_array).ToList();
            case FonTypeCode.Short:
                return NativeApi.GetArray<short>(raw, keyBytes, NativeBindings.fon_collection_get_short_array).ToList();
            case FonTypeCode.Int:
                return NativeApi.GetArray<int>(raw, keyBytes, NativeBindings.fon_collection_get_int_array).ToList();
            case FonTypeCode.UInt:
                return NativeApi.GetArray<uint>(raw, keyBytes, NativeBindings.fon_collection_get_uint_array).ToList();
            case FonTypeCode.Long:
                return NativeApi.GetArray<long>(raw, keyBytes, NativeBindings.fon_collection_get_long_array).ToList();
            case FonTypeCode.ULong:
                return NativeApi.GetArray<ulong>(raw, keyBytes, NativeBindings.fon_collection_get_ulong_array).ToList();
            case FonTypeCode.Float:
                return NativeApi.GetArray<float>(raw, keyBytes, NativeBindings.fon_collection_get_float_array).ToList();
            case FonTypeCode.Double:
                return NativeApi.GetArray<double>(raw, keyBytes, NativeBindings.fon_collection_get_double_array).ToList();
            case FonTypeCode.Bool:
                return NativeApi.GetArray<byte>(raw, keyBytes, NativeBindings.fon_collection_get_bool_array).Select(item => item != 0).ToList();
            case FonTypeCode.String:
                return NativeApi.GetStringArray(raw, keyBytes).ToList();
            case FonTypeCode.Object:
                return CreateChildViews(NativeApi.GetChildren(raw, keyBytes));
            default:
                throw new NotSupportedException($"Unknown native type tag '{typeChar}' (array=true)");
        }
    }


    private unsafe void PutValue(string key, object value, int mode) {
        switch (value) {
            case byte v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_byte);
                break;
            case short v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_short);
                break;
            case int v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_int);
                break;
            case uint v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_uint);
                break;
            case long v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_long);
                break;
            case ulong v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_ulong);
                break;
            case float v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_float);
                break;
            case double v:
                PutScalar(key, v, mode, NativeBindings.fon_collection_put_double);
                break;
            case bool v:
                PutScalar(key, v ? 1 : 0, mode, NativeBindings.fon_collection_put_bool);
                break;
            case string v:
                PutBytes(key, NativeApi.Encode(v), mode, NativeBindings.fon_collection_put_string);
                break;
            case RawData v:
                PutBytes(key, v.ToUnpackedBytes(), mode, NativeBindings.fon_collection_put_raw);
                break;
            case FonCollection v:
                PutChild(key, v, mode);
                break;
            case List<byte> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_byte_array);
                break;
            case List<short> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_short_array);
                break;
            case List<int> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_int_array);
                break;
            case List<uint> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_uint_array);
                break;
            case List<long> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_long_array);
                break;
            case List<ulong> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_ulong_array);
                break;
            case List<float> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_float_array);
                break;
            case List<double> v:
                PutArray(key, v.ToArray(), mode, NativeBindings.fon_collection_put_double_array);
                break;
            case List<bool> v:
                PutArray(key, v.Select(item => (byte)(item ? 1 : 0)).ToArray(), mode, NativeBindings.fon_collection_put_bool_array);
                break;
            case List<string> v:
                PutStringArray(key, v.ToArray(), mode);
                break;
            case List<FonCollection> v:
                PutChildren(key, v, mode);
                break;
            case List<RawData>:
                throw new NotSupportedException("Arrays of RawData are not supported by FonCollection.");
            case null:
                throw new ArgumentNullException(nameof(value));
            default:
                throw new NotSupportedException($"Unsupported type for FON serialization: {value.GetType().FullName}");
        }
    }


    private void PutScalar<T>(string key, T value, int mode, ScalarPut<T> put) where T : unmanaged {
        byte[] keyBytes = NativeApi.Encode(key);
        Mutate(raw => NativeApi.PutScalar(raw, keyBytes, value, mode, put));
    }


    private void PutBytes(string key, byte[] value, int mode, BytesPut put) {
        byte[] keyBytes = NativeApi.Encode(key);
        Mutate(raw => NativeApi.PutBytes(raw, keyBytes, value, mode, put));
    }


    private void PutArray<T>(string key, T[] values, int mode, ArrayPut<T> put) where T : unmanaged {
        byte[] keyBytes = NativeApi.Encode(key);
        Mutate(raw => NativeApi.PutArray(raw, keyBytes, values, mode, put));
    }


    private void PutStringArray(string key, string[] items, int mode) {
        byte[] keyBytes = NativeApi.Encode(key);
        Mutate(raw => NativeApi.PutStringArray(raw, keyBytes, items, mode));
    }


    private void PutChild(string key, FonCollection child, int mode) {
        byte[] keyBytes = NativeApi.Encode(key);
        List<FonCollection> children = new List<FonCollection> { child };
        Mutate(raw => {
            ValidateChildren(children);
            NativeApi.PutChild(raw, keyBytes, child.OwnedPointer(), mode);
        });
        TransferChildren(children);
    }


    private void PutChildren(string key, IReadOnlyList<FonCollection> children, int mode) {
        byte[] keyBytes = NativeApi.Encode(key);
        Mutate(raw => {
            ValidateChildren(children);
            IntPtr[] pointers = children.Select(item => item.OwnedPointer()).ToArray();
            NativeApi.PutChildren(raw, keyBytes, pointers, mode);
        });
        TransferChildren(children);
    }
}
