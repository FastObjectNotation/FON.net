using System.Collections;
using FON.Native;

namespace FON.Types;


/// <summary>
/// Records keyed by id (0-based line number). Backed by a native <c>fon_dump_*</c>
/// handle — every operation routes through <see cref="NativeBindings"/>.
/// </summary>
/// <remarks>
/// The native store is append-only: <see cref="Remove"/> and overwriting an existing
/// id via the indexer are not supported and throw. A <see cref="FonCollection"/>
/// obtained from this dump (via the indexer, <see cref="Get"/>, <see cref="TryGet"/>,
/// or enumeration) is a borrowed view into the dump's native memory — do not use it
/// after this <see cref="FonDump"/> is disposed.
/// </remarks>
public sealed class FonDump : IEnumerable<KeyValuePair<ulong, FonCollection>>, IDisposable {
    private const long EmptyDumpBytesEstimate = 64;

    private readonly FonDumpHandle handle;


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


    public FonCollection this[ulong index] {
        get => Get(index);
        set {
            ThrowIfDisposed();
            if (Exists(index)) {
                throw new InvalidOperationException($"FonCollection with id {index} already exists in the FON dump");
            }
            AddCore(index, value);
        }
    }


    public int Count {
        get {
            ThrowIfDisposed();
            IntPtr raw = handle.DangerousGetHandle();
            long size = NativeBindings.fon_dump_size(raw);
            GC.KeepAlive(handle);
            return checked((int)size);
        }
    }


    public void Dispose() {
        handle.Dispose();
    }


    public IEnumerator<KeyValuePair<ulong, FonCollection>> GetEnumerator() {
        ThrowIfDisposed();
        IntPtr raw = handle.DangerousGetHandle();
        long size = NativeBindings.fon_dump_size(raw);
        GC.KeepAlive(handle);

        for (long i = 0; i < size; i++) {
            var error = new FonError();
            IntPtr rawForId = handle.DangerousGetHandle();
            int rc = NativeBindings.fon_dump_id_at(rawForId, i, out ulong id);
            GC.KeepAlive(handle);
            if (rc != FonResultCode.OK) {
                throw new FonNativeException(error);
            }
            yield return new KeyValuePair<ulong, FonCollection>(id, Get(id));
        }
    }


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(ulong id, FonCollection value) {
        ThrowIfDisposed();
        if (Exists(id)) {
            throw new InvalidOperationException($"FonCollection with id {id} already exists in the FON dump");
        }
        AddCore(id, value);
    }


    public bool TryAdd(ulong id, FonCollection value) {
        ThrowIfDisposed();
        if (Exists(id)) {
            return false;
        }
        AddCore(id, value);
        return true;
    }


    /// <remarks>
    /// Not supported: the native store is append-only and has no removal primitive.
    /// Always throws <see cref="NotSupportedException"/>.
    /// </remarks>
    public bool Remove(ulong id) {
        throw new NotSupportedException("Remove is not supported by the native-backed FonDump store; the underlying native dump is append-only.");
    }


    public FonCollection Get(ulong id) {
        ThrowIfDisposed();
        IntPtr raw = handle.DangerousGetHandle();
        IntPtr childRaw = NativeBindings.fon_dump_get(raw, id);
        GC.KeepAlive(handle);
        if (childRaw == IntPtr.Zero) {
            throw new KeyNotFoundException($"No FonCollection with id {id} in the FON dump");
        }
        return FonCollection.FromBorrowedHandle(childRaw);
    }


    public FonCollection? TryGet(ulong id) {
        ThrowIfDisposed();
        IntPtr raw = handle.DangerousGetHandle();
        IntPtr childRaw = NativeBindings.fon_dump_get(raw, id);
        GC.KeepAlive(handle);
        return childRaw == IntPtr.Zero ? null : FonCollection.FromBorrowedHandle(childRaw);
    }


    internal static FonDump FromOwnedHandle(IntPtr raw, long estimatedBytes) {
        return new FonDump(new FonDumpHandle(raw, ownsHandle: true, estimatedBytes));
    }


    internal FonDumpHandle Handle => handle;


    private static FonDumpHandle CreateOwnHandle() {
        IntPtr raw = NativeBindings.fon_dump_create();
        return new FonDumpHandle(raw, ownsHandle: true, EmptyDumpBytesEstimate);
    }


    private void ThrowIfDisposed() {
        if (handle.IsClosed) {
            throw new ObjectDisposedException(nameof(FonDump));
        }
    }


    private bool Exists(ulong id) {
        IntPtr raw = handle.DangerousGetHandle();
        IntPtr found = NativeBindings.fon_dump_get(raw, id);
        GC.KeepAlive(handle);
        return found != IntPtr.Zero;
    }


    private void AddCore(ulong id, FonCollection value) {
        if (value == null) {
            throw new ArgumentNullException(nameof(value));
        }
        value.ThrowIfDisposed();

        var error = new FonError();
        IntPtr dumpRaw = handle.DangerousGetHandle();
        IntPtr childRaw = value.Handle.DangerousGetHandle();
        int rc;
        try {
            rc = NativeBindings.fon_dump_add(dumpRaw, id, childRaw, ref error);
        } finally {
            GC.KeepAlive(handle);
            GC.KeepAlive(value.Handle);
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
        handle.AdoptChild(value.Handle);
    }
}
