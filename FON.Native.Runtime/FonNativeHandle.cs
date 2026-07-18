using Microsoft.Win32.SafeHandles;

namespace FON.Native;


/// <summary>
/// Base for SafeHandle wrappers around a native FON pointer (a Dump or a Collection).
/// Tracks an approximate native-memory-pressure charge via <see cref="GC.AddMemoryPressure"/>
/// so the GC collects handle-owning objects promptly even when the caller never calls
/// <see cref="IDisposable.Dispose"/>, and supports ownership hand-off for the native APIs
/// that take ownership of a child handle (e.g. nesting a Collection inside another
/// Collection or a Dump).
/// </summary>
public abstract class FonNativeHandle : SafeHandleZeroOrMinusOneIsInvalid {
    private long trackedBytes;


    protected FonNativeHandle(IntPtr handle, bool ownsHandle, long estimatedBytes) : base(ownsHandle) {
        SetHandle(handle);
        trackedBytes = ownsHandle && estimatedBytes > 0 ? estimatedBytes : 0;
        if (trackedBytes > 0) {
            GC.AddMemoryPressure(trackedBytes);
        }
    }


    protected abstract void FreeNative(IntPtr handle);


    /// <summary>
    /// Takes over the memory-pressure accounting of <paramref name="child"/> after its native
    /// memory has just been adopted into this handle's tree (a successful native "add
    /// collection"/"dump add" call transfers ownership). The child handle is invalidated —
    /// it must never be used or freed again; freeing now happens transitively when this
    /// handle (or whichever handle eventually owns it) is released.
    /// </summary>
    public void AdoptChild(FonNativeHandle child) {
        long bytes = child.DetachOwnership();
        if (bytes > 0) {
            trackedBytes += bytes;
            GC.AddMemoryPressure(bytes);
        }
    }


    private long DetachOwnership() {
        long bytes = trackedBytes;
        if (bytes > 0) {
            GC.RemoveMemoryPressure(bytes);
            trackedBytes = 0;
        }
        SetHandleAsInvalid();
        return bytes;
    }


    protected sealed override bool ReleaseHandle() {
        FreeNative(handle);
        if (trackedBytes > 0) {
            GC.RemoveMemoryPressure(trackedBytes);
            trackedBytes = 0;
        }
        return true;
    }
}


/// <summary>SafeHandle for a native <c>fon_collection_*</c> pointer.</summary>
public sealed class FonCollectionHandle : FonNativeHandle {
    public FonCollectionHandle(IntPtr handle, bool ownsHandle, long estimatedBytes = 0) : base(handle, ownsHandle, estimatedBytes) {
    }


    protected override void FreeNative(IntPtr handle) => NativeBindings.fon_collection_free(handle);
}


/// <summary>SafeHandle for a native <c>fon_dump_*</c> pointer.</summary>
public sealed class FonDumpHandle : FonNativeHandle {
    public FonDumpHandle(IntPtr handle, bool ownsHandle, long estimatedBytes = 0) : base(handle, ownsHandle, estimatedBytes) {
    }


    protected override void FreeNative(IntPtr handle) => NativeBindings.fon_dump_free(handle);
}
