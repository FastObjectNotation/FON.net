using System.Collections;
using FON.Native;

namespace FON.Types;


/// <summary>
/// Records keyed by id. Backed by a native dump.
/// </summary>
/// <remarks>
/// Ids are stable in memory and are renumbered when the dump is serialized. A
/// <see cref="FonCollection"/> read from this dump (by indexer, <see cref="Get"/>,
/// <see cref="TryGet"/> or enumeration) is a view: it keeps this dump alive, and it throws
/// <see cref="FonStaleViewException"/> once its record was replaced or removed, or a field of
/// its record changed (for nested views). Views of other ids stay valid. Disposing the dump
/// makes every view throw <see cref="ObjectDisposedException"/>.
/// </remarks>
public sealed class FonDump : IEnumerable<KeyValuePair<ulong, FonCollection>>, IDisposable {
    private const long EmptyDumpBytesEstimate = 64;

    private readonly FonDumpHandle handle;
    internal long version;


    public FonDump() : this(CreateOwnHandle()) {
    }


    /// <remarks>
    /// The native dump has no capacity-hint primitive; <paramref name="capacity"/> is
    /// accepted for source compatibility but otherwise unused.
    /// </remarks>
    public FonDump(int capacity) : this(CreateOwnHandle()) {
    }


    internal FonDump(FonDumpHandle handle) {
        this.handle = handle;
    }


    /// <summary>Record with the id. Setting it stores the record (upsert).</summary>
    public FonCollection this[ulong index] {
        get => Get(index);
        set => Set(index, value);
    }


    /// <summary>Number of live records.</summary>
    public int Count => Read(raw => checked((int)NativeBindings.fon_dump_size(raw)));


    /// <summary>All live ids in ascending order.</summary>
    public IReadOnlyList<ulong> Ids => Read(raw => NativeApi.ReadDumpIds(raw));


    public void Dispose() {
        handle.Dispose();
    }


    public IEnumerator<KeyValuePair<ulong, FonCollection>> GetEnumerator() {
        foreach (ulong id in Ids) {
            yield return new KeyValuePair<ulong, FonCollection>(id, Get(id));
        }
    }


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    /// <summary>Adds a record under a new id. Throws <see cref="InvalidOperationException"/> if the id exists.</summary>
    public void Add(ulong id, FonCollection value) {
        PutRecord(id, value, FonPutMode.Add);
    }


    /// <summary>Adds a record under a new id; returns false if the id exists.</summary>
    public bool TryAdd(ulong id, FonCollection value) {
        if (ContainsId(id)) {
            return false;
        }
        PutRecord(id, value, FonPutMode.Add);
        return true;
    }


    /// <summary>Stores a record under the id, replacing any existing record (upsert).</summary>
    public void Set(ulong id, FonCollection value) {
        PutRecord(id, value, FonPutMode.Upsert);
    }


    /// <summary>Replaces an existing record. Throws <see cref="KeyNotFoundException"/> if the id is absent.</summary>
    public void Replace(ulong id, FonCollection value) {
        PutRecord(id, value, FonPutMode.Set);
    }


    /// <summary>Removes the record with the id. Returns false if it was absent.</summary>
    public bool Remove(ulong id) {
        bool removed = Read(raw => NativeApi.DumpRemove(raw, id));
        // Even an absent id may move record storage in the native dump, so views must re-resolve.
        version++;
        return removed;
    }


    /// <summary>Removes the records with the ids. Absent and duplicate ids are ignored.</summary>
    /// <returns>The number of records removed.</returns>
    public int RemoveMany(IEnumerable<ulong> ids) {
        ArgumentNullException.ThrowIfNull(ids);
        ulong[] array = ids.ToArray();
        long removed = Read(raw => NativeApi.DumpRemoveMany(raw, array));
        // Same as Remove: the native call may move record storage even when nothing is removed.
        version++;
        return checked((int)removed);
    }


    public bool ContainsId(ulong id) {
        return Read(raw => NativeApi.DumpGet(raw, id) != IntPtr.Zero);
    }


    /// <summary>Record with the id. Throws <see cref="KeyNotFoundException"/> if it is absent.</summary>
    public FonCollection Get(ulong id) {
        FonCollection? found = TryGet(id);
        if (found is null) {
            throw new KeyNotFoundException($"No FonCollection with id {id} in the FON dump");
        }
        return found;
    }


    /// <summary>Record with the id, or null if it is absent.</summary>
    public FonCollection? TryGet(ulong id) {
        IntPtr pointer = GetRecordPointer(id);
        if (pointer == IntPtr.Zero) {
            return null;
        }
        if (!TryGetGens(id, out uint recordGen, out uint treeGen)) {
            return null;
        }
        return new FonCollection(new FonCollectionView {
            dump = this,
            recordId = id,
            recordGen = recordGen,
            treeGen = treeGen,
            isRecord = true,
            capturedVersion = version,
            pointer = pointer
        });
    }



    // ==================== INTERNAL ====================

    internal FonDumpHandle Handle => handle;


    internal static FonDump FromOwnedHandle(IntPtr raw, long estimatedBytes) {
        return new FonDump(new FonDumpHandle(raw, ownsHandle: true, estimatedBytes));
    }


    /// <summary>Validated native pointer of this dump; throws if the dump is disposed.</summary>
    internal IntPtr Access() {
        if (handle.IsClosed) {
            throw new ObjectDisposedException(nameof(FonDump));
        }
        return handle.DangerousGetHandle();
    }


    internal T Read<T>(Func<IntPtr, T> read) {
        IntPtr raw = Access();
        try {
            return read(raw);
        } finally {
            GC.KeepAlive(this);
        }
    }


    internal void Use(Action<IntPtr> use) {
        IntPtr raw = Access();
        try {
            use(raw);
        } finally {
            GC.KeepAlive(this);
        }
    }


    /// <summary>Record generation and tree generation of a record; false if the id is absent.</summary>
    internal bool TryGetGens(ulong id, out uint recordGen, out uint treeGen) {
        IntPtr raw = Access();
        try {
            return NativeApi.DumpGens(raw, id, out recordGen, out treeGen);
        } finally {
            GC.KeepAlive(this);
        }
    }


    internal IntPtr GetRecordPointer(ulong id) {
        return Read(raw => NativeApi.DumpGet(raw, id));
    }


    /// <summary>Marks a mutation inside record <paramref name="id"/> (fon_dump_touch).</summary>
    internal void Touch(ulong id) {
        Use(raw => NativeApi.DumpTouch(raw, id));
    }



    // ==================== PRIVATE ====================

    private static FonDumpHandle CreateOwnHandle() {
        IntPtr raw = NativeBindings.fon_dump_create();
        return new FonDumpHandle(raw, ownsHandle: true, EmptyDumpBytesEstimate);
    }


    private void PutRecord(ulong id, FonCollection value, int mode) {
        FonCollection.ValidateOwnedRoot(value);
        IntPtr raw = Access();
        try {
            NativeApi.DumpPut(raw, id, value.OwnedPointer(), mode);
        } finally {
            GC.KeepAlive(this);
        }
        value.TransferTo(handle);
        version++;
    }
}
