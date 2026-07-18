using System.Collections;
using System.Reflection;
using System.Text;
using FON.Native;

namespace FON.Types;


/// <summary>
/// Key-value collection that can hold primitives, strings, RawData, and other
/// FonCollections (nested objects) or lists of any supported type. Backed by a
/// native <c>fon_collection_*</c> handle — every operation routes through
/// <see cref="NativeBindings"/>.
/// </summary>
/// <remarks>
/// Cycles are the caller's responsibility: placing a FonCollection inside
/// itself (directly or transitively through nested values or lists) is not
/// detected beyond the immediate self-reference check. Serialization will
/// recurse until the call stack overflows.
///
/// The native store is append-only: <see cref="Remove"/> and overwriting an
/// existing key via the indexer are not supported and throw. A
/// <see cref="FonCollection"/> obtained via <see cref="Get{T}"/>, a list
/// element, or dump/collection enumeration is a borrowed view into its
/// parent's native memory — do not use it after the parent is disposed.
/// </remarks>
public sealed class FonCollection : IEnumerable<KeyValuePair<string, object>>, IDisposable {
    private const long EmptyCollectionBytesEstimate = 64;
    private const int KeyBufferSize = 512;
    private const int StringBufferSize = 8192;

    private static readonly UTF8Encoding utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly FonCollectionHandle handle;


    public FonCollection() : this(CreateOwnHandle()) {
    }


    internal FonCollection(FonCollectionHandle handle) {
        this.handle = handle;
    }


    public object this[string key] {
        get {
            ThrowIfDisposed();
            int kind = KindOf(key);
            if (kind < 0) {
                throw new KeyNotFoundException($"Key '{key}' not found in FonCollection");
            }
            return GetBoxedValue(key, kind);
        }
        set {
            ThrowIfDisposed();
            if (KindOf(key) >= 0) {
                throw new InvalidOperationException($"Object with key {key} already exists in the collection");
            }
            AddCore(key, value);
        }
    }


    public void Dispose() {
        handle.Dispose();
    }


    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
        ThrowIfDisposed();
        IntPtr raw = handle.DangerousGetHandle();
        long size = NativeBindings.fon_collection_size(raw);
        GC.KeepAlive(handle);

        for (long i = 0; i < size; i++) {
            string key = KeyAt(i);
            int kind = KindOf(key);
            yield return new KeyValuePair<string, object>(key, GetBoxedValue(key, kind));
        }
    }


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(string key, object value) {
        ThrowIfDisposed();
        if (KindOf(key) >= 0) {
            throw new InvalidOperationException($"Object with key {key} already exists in the collection");
        }
        AddCore(key, value);
    }


    public bool TryAdd(string key, object value) {
        ThrowIfDisposed();
        if (KindOf(key) >= 0) {
            return false;
        }
        AddCore(key, value);
        return true;
    }


    /// <remarks>
    /// Not supported: the native store is append-only and has no removal primitive.
    /// Always throws <see cref="NotSupportedException"/>.
    /// </remarks>
    public bool Remove(string key) {
        throw new NotSupportedException("Remove is not supported by the native-backed FonCollection store; the underlying native collection is append-only.");
    }


    public object Get(string key) => this[key];


    public T Get<T>(string key) => (T)this[key];


    public object? TryGet(string key) {
        ThrowIfDisposed();
        int kind = KindOf(key);
        return kind < 0 ? null : GetBoxedValue(key, kind);
    }


    public T? TryGet<T>(string key) where T : class => TryGet(key) as T;


    public T? TryGetNullable<T>(string key) where T : struct => TryGet(key) is T value ? value : null;




    public static FonCollection Serialize<T>(T obj) {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var collection = new FonCollection();

        foreach (var property in properties) {
            var value = property.GetValue(obj);
            if (value != null) {
                collection.Add(property.Name, value);
            }
        }

        return collection;
    }




    public T Deserialize<T>() where T : new() {
        ThrowIfDisposed();
        T result = new T();
        object boxedResult = result;

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties) {
            var value = TryGet(property.Name);
            if (value != null && property.PropertyType.IsInstanceOfType(value)) {
                property.SetValue(boxedResult, value);
            }
        }

        return (T)boxedResult;
    }




    internal static FonCollection FromOwnedHandle(IntPtr raw, long estimatedBytes) {
        return new FonCollection(new FonCollectionHandle(raw, ownsHandle: true, estimatedBytes));
    }


    internal static FonCollection FromBorrowedHandle(IntPtr raw) {
        return new FonCollection(new FonCollectionHandle(raw, ownsHandle: false));
    }


    internal FonCollectionHandle Handle => handle;


    private static FonCollectionHandle CreateOwnHandle() {
        IntPtr raw = NativeBindings.fon_collection_create();
        return new FonCollectionHandle(raw, ownsHandle: true, EmptyCollectionBytesEstimate);
    }


    internal void ThrowIfDisposed() {
        if (handle.IsClosed) {
            throw new ObjectDisposedException(nameof(FonCollection));
        }
    }


    private int KindOf(string key) {
        IntPtr raw = handle.DangerousGetHandle();
        int kind = NativeBindings.fon_collection_kind_of(raw, key);
        GC.KeepAlive(handle);
        return kind;
    }


    private string KeyAt(long index) {
        var error = new FonError();
        var buffer = new byte[KeyBufferSize];
        IntPtr raw = handle.DangerousGetHandle();
        int rc = NativeBindings.fon_collection_key_at(raw, index, buffer, buffer.Length, ref error);
        GC.KeepAlive(handle);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        return DecodeCString(buffer);
    }


    private static string DecodeCString(byte[] buffer) {
        int len = Array.IndexOf(buffer, (byte)0);
        return utf8NoBom.GetString(buffer, 0, len < 0 ? buffer.Length : len);
    }


    // ==================== ADD DISPATCH ====================

    private delegate int AddScalarFn<T>(IntPtr collection, string key, T value, ref FonError error);
    private delegate int AddArrayFn<T>(IntPtr collection, string key, T[] values, long count, ref FonError error);


    private void AddCore(string key, object value) {
        switch (value) {
            case byte v: AddScalar(key, v, NativeBindings.fon_collection_add_byte); break;
            case short v: AddScalar(key, v, NativeBindings.fon_collection_add_short); break;
            case int v: AddScalar(key, v, NativeBindings.fon_collection_add_int); break;
            case uint v: AddScalar(key, v, NativeBindings.fon_collection_add_uint); break;
            case long v: AddScalar(key, v, NativeBindings.fon_collection_add_long); break;
            case ulong v: AddScalar(key, v, NativeBindings.fon_collection_add_ulong); break;
            case float v: AddScalar(key, v, NativeBindings.fon_collection_add_float); break;
            case double v: AddScalar(key, v, NativeBindings.fon_collection_add_double); break;
            case bool v: AddScalar(key, v ? 1 : 0, NativeBindings.fon_collection_add_bool); break;
            case string v: AddScalar(key, v, NativeBindings.fon_collection_add_string); break;
            case RawData v: AddRaw(key, v); break;
            case FonCollection v: AddNestedCollection(key, v); break;
            case List<byte> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_byte_array); break;
            case List<short> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_short_array); break;
            case List<int> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_int_array); break;
            case List<uint> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_uint_array); break;
            case List<long> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_long_array); break;
            case List<ulong> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_ulong_array); break;
            case List<float> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_float_array); break;
            case List<double> v: AddFixedArray(key, v, NativeBindings.fon_collection_add_double_array); break;
            case List<bool> v: AddFixedArray(key, v.ConvertAll(b => (byte)(b ? 1 : 0)), NativeBindings.fon_collection_add_bool_array); break;
            case List<string> v: AddStringArray(key, v); break;
            case List<FonCollection> v: AddCollectionArray(key, v); break;
            case List<RawData>: throw new NotSupportedException("Arrays of RawData are not supported by the native-backed FonCollection store.");
            case null: throw new ArgumentNullException(nameof(value));
            default: throw new NotSupportedException($"Unsupported type for FON serialization: {value.GetType().FullName}");
        }
    }


    private void AddScalar<T>(string key, T value, AddScalarFn<T> fn) {
        var error = new FonError();
        IntPtr raw = handle.DangerousGetHandle();
        int rc;
        try {
            rc = fn(raw, key, value, ref error);
        } finally {
            GC.KeepAlive(handle);
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
    }


    private void AddFixedArray<T>(string key, List<T> values, AddArrayFn<T> fn) {
        var array = values.ToArray();
        var error = new FonError();
        IntPtr raw = handle.DangerousGetHandle();
        int rc;
        try {
            rc = fn(raw, key, array, array.LongLength, ref error);
        } finally {
            GC.KeepAlive(handle);
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
    }


    private void AddStringArray(string key, List<string> values) {
        // string[] cannot be marshalled directly to LPUTF8Str[]; allocate one
        // nul-terminated UTF-8 buffer per element and pass pointers instead.
        var pointers = new IntPtr[values.Count];
        try {
            for (int i = 0; i < values.Count; i++) {
                pointers[i] = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(values[i]);
            }

            var error = new FonError();
            IntPtr raw = handle.DangerousGetHandle();
            int rc;
            try {
                rc = NativeBindings.fon_collection_add_string_array(raw, key, pointers, pointers.LongLength, ref error);
            } finally {
                GC.KeepAlive(handle);
            }
            if (rc != FonResultCode.OK) {
                throw new FonNativeException(error);
            }
        } finally {
            foreach (var pointer in pointers) {
                if (pointer != IntPtr.Zero) {
                    System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pointer);
                }
            }
        }
    }


    private unsafe void AddRaw(string key, RawData value) {
        var bytes = value.ToUnpackedBytes();
        var error = new FonError();
        IntPtr raw = handle.DangerousGetHandle();
        int rc;
        fixed (byte* p = bytes) {
            try {
                rc = NativeBindings.fon_collection_add_raw(raw, key, bytes.Length == 0 ? null : p, bytes.Length, ref error);
            } finally {
                GC.KeepAlive(handle);
            }
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
    }


    private void AddNestedCollection(string key, FonCollection child) {
        if (ReferenceEquals(child, this)) {
            throw new InvalidOperationException("A FonCollection cannot contain itself");
        }
        child.ThrowIfDisposed();

        var error = new FonError();
        IntPtr parentRaw = handle.DangerousGetHandle();
        IntPtr childRaw = child.handle.DangerousGetHandle();
        int rc;
        try {
            rc = NativeBindings.fon_collection_add_collection(parentRaw, key, childRaw, ref error);
        } finally {
            GC.KeepAlive(handle);
            GC.KeepAlive(child.handle);
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        handle.AdoptChild(child.handle);
    }


    private void AddCollectionArray(string key, List<FonCollection> children) {
        foreach (var child in children) {
            if (ReferenceEquals(child, this)) {
                throw new InvalidOperationException("A FonCollection cannot contain itself");
            }
            child.ThrowIfDisposed();
        }

        var childRaws = new IntPtr[children.Count];
        for (int i = 0; i < children.Count; i++) {
            childRaws[i] = children[i].handle.DangerousGetHandle();
        }

        var error = new FonError();
        IntPtr parentRaw = handle.DangerousGetHandle();
        int rc;
        try {
            rc = NativeBindings.fon_collection_add_collection_array(parentRaw, key, childRaws, childRaws.LongLength, ref error);
        } finally {
            GC.KeepAlive(handle);
            foreach (var child in children) {
                GC.KeepAlive(child.handle);
            }
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        foreach (var child in children) {
            handle.AdoptChild(child.handle);
        }
    }


    // ==================== GET DISPATCH ====================

    private delegate int GetScalarFn<T>(IntPtr collection, string key, out T value, ref FonError error);
    private delegate int GetArrayFn<T>(IntPtr collection, string key, T[]? buffer, long bufferSize, out long actualSize, ref FonError error);


    private object GetBoxedValue(string key, int kind) {
        bool isArray = (kind & 0x100) != 0;
        char typeChar = (char)(kind & 0xFF);
        IntPtr raw = handle.DangerousGetHandle();
        try {
            return (isArray, typeChar) switch {
                (false, 'e') => GetScalar<byte>(raw, key, NativeBindings.fon_collection_get_byte),
                (false, 't') => GetScalar<short>(raw, key, NativeBindings.fon_collection_get_short),
                (false, 'i') => GetScalar<int>(raw, key, NativeBindings.fon_collection_get_int),
                (false, 'u') => GetScalar<uint>(raw, key, NativeBindings.fon_collection_get_uint),
                (false, 'l') => GetScalar<long>(raw, key, NativeBindings.fon_collection_get_long),
                (false, 'g') => GetScalar<ulong>(raw, key, NativeBindings.fon_collection_get_ulong),
                (false, 'f') => GetScalar<float>(raw, key, NativeBindings.fon_collection_get_float),
                (false, 'd') => GetScalar<double>(raw, key, NativeBindings.fon_collection_get_double),
                (false, 'b') => GetScalar<int>(raw, key, NativeBindings.fon_collection_get_bool) != 0,
                (false, 's') => GetScalarString(raw, key),
                (false, 'r') => GetRaw(raw, key),
                (false, 'o') => GetNestedCollection(raw, key),
                (true, 'e') => GetFixedArray<byte>(raw, key, NativeBindings.fon_collection_get_byte_array),
                (true, 't') => GetFixedArray<short>(raw, key, NativeBindings.fon_collection_get_short_array),
                (true, 'i') => GetFixedArray<int>(raw, key, NativeBindings.fon_collection_get_int_array),
                (true, 'u') => GetFixedArray<uint>(raw, key, NativeBindings.fon_collection_get_uint_array),
                (true, 'l') => GetFixedArray<long>(raw, key, NativeBindings.fon_collection_get_long_array),
                (true, 'g') => GetFixedArray<ulong>(raw, key, NativeBindings.fon_collection_get_ulong_array),
                (true, 'f') => GetFixedArray<float>(raw, key, NativeBindings.fon_collection_get_float_array),
                (true, 'd') => GetFixedArray<double>(raw, key, NativeBindings.fon_collection_get_double_array),
                (true, 'b') => GetFixedArray<byte>(raw, key, NativeBindings.fon_collection_get_bool_array).ConvertAll(b => b != 0),
                (true, 's') => GetStringArray(raw, key),
                (true, 'o') => GetCollectionArray(raw, key),
                _ => throw new NotSupportedException($"Unknown native type tag '{typeChar}' (array={isArray})")
            };
        } finally {
            GC.KeepAlive(handle);
        }
    }


    private static T GetScalar<T>(IntPtr raw, string key, GetScalarFn<T> fn) {
        var error = new FonError();
        int rc = fn(raw, key, out T value, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        return value;
    }


    private static List<T> GetFixedArray<T>(IntPtr raw, string key, GetArrayFn<T> fn) {
        var error = new FonError();
        int rc = fn(raw, key, null, 0, out long size, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        if (size == 0) {
            return new List<T>();
        }
        var buffer = new T[size];
        rc = fn(raw, key, buffer, size, out _, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        return new List<T>(buffer);
    }


    private static string GetScalarString(IntPtr raw, string key) {
        var error = new FonError();
        var buffer = new byte[StringBufferSize];
        int rc = NativeBindings.fon_collection_get_string(raw, key, buffer, buffer.Length, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        return DecodeCString(buffer);
    }


    private static List<string> GetStringArray(IntPtr raw, string key) {
        var error = new FonError();
        int rc = NativeBindings.fon_collection_get_string_array_count(raw, key, out long count, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        var list = new List<string>((int)count);
        var buffer = new byte[StringBufferSize];
        for (long i = 0; i < count; i++) {
            rc = NativeBindings.fon_collection_get_string_array_item(raw, key, i, buffer, buffer.Length, ref error);
            if (rc != FonResultCode.OK) {
                throw new FonNativeException(error);
            }
            list.Add(DecodeCString(buffer));
        }
        return list;
    }


    private static RawData GetRaw(IntPtr raw, string key) {
        var error = new FonError();
        int rc = NativeBindings.fon_collection_get_raw(raw, key, null, 0, out long size, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        if (size == 0) {
            return new RawData(Array.Empty<byte>());
        }
        var buffer = new byte[size];
        rc = NativeBindings.fon_collection_get_raw(raw, key, buffer, size, out _, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        return new RawData(buffer);
    }


    private static FonCollection GetNestedCollection(IntPtr raw, string key) {
        var error = new FonError();
        IntPtr childRaw = NativeBindings.fon_collection_get_collection(raw, key, ref error);
        if (childRaw == IntPtr.Zero) {
            throw new FonNativeException(error);
        }
        return FromBorrowedHandle(childRaw);
    }


    private static List<FonCollection> GetCollectionArray(IntPtr raw, string key) {
        var error = new FonError();
        int rc = NativeBindings.fon_collection_get_collection_array(raw, key, null, 0, out long size, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        var result = new List<FonCollection>((int)size);
        if (size == 0) {
            return result;
        }
        var buffer = new IntPtr[size];
        rc = NativeBindings.fon_collection_get_collection_array(raw, key, buffer, size, out _, ref error);
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        foreach (var childRaw in buffer) {
            result.Add(FromBorrowedHandle(childRaw));
        }
        return result;
    }
}
