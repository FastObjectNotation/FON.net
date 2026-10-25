using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Raw ABI tests for the global nesting limit: the boundary itself and the clamp of
/// non-positive values to a limit of one.
/// </summary>
public unsafe class NativeMaxDepthTests {
    private const int DefaultMaxDepth = 64;


    [Fact]
    public void NativeMaxDepth_AtLimit_Succeeds() {
        byte[] text = SerializeOneRecordDump(BuildNativeNested(5));
        try {
            NativeBindings.fon_set_max_depth(5);

            IntPtr dump = TryParseDump(text, out int code);

            Assert.NotEqual(IntPtr.Zero, dump);
            Assert.Equal(FonResultCode.OK, code);
            NativeBindings.fon_dump_free(dump);
        } finally {
            NativeBindings.fon_set_max_depth(DefaultMaxDepth);
        }
    }


    [Fact]
    public void NativeMaxDepth_BeyondLimit_ParseFails() {
        byte[] text = SerializeOneRecordDump(BuildNativeNested(6));
        try {
            NativeBindings.fon_set_max_depth(5);

            IntPtr dump = TryParseDump(text, out int code);

            Assert.Equal(IntPtr.Zero, dump);
            Assert.Equal(FonResultCode.ParseFailed, code);
        } finally {
            NativeBindings.fon_set_max_depth(DefaultMaxDepth);
        }
    }


    [Fact]
    public void NativeMaxDepth_NegativeValue_ClampedToOne() {
        byte[] oneLevel = SerializeOneRecordDump(BuildNativeNested(1));
        byte[] twoLevels = SerializeOneRecordDump(BuildNativeNested(2));
        try {
            NativeBindings.fon_set_max_depth(-3);

            IntPtr accepted = TryParseDump(oneLevel, out int acceptedCode);
            IntPtr rejected = TryParseDump(twoLevels, out int rejectedCode);

            Assert.NotEqual(IntPtr.Zero, accepted);
            Assert.Equal(FonResultCode.OK, acceptedCode);
            NativeBindings.fon_dump_free(accepted);
            Assert.Equal(IntPtr.Zero, rejected);
            Assert.Equal(FonResultCode.ParseFailed, rejectedCode);
        } finally {
            NativeBindings.fon_set_max_depth(DefaultMaxDepth);
        }
    }


    /// <summary>A collection with the given number of wrapping levels above a leaf field. Owned by the caller.</summary>
    private static IntPtr BuildNativeNested(int wraps) {
        IntPtr current = NativeBindings.fon_collection_create();
        FonError error = default;
        Assert.Equal(FonResultCode.OK, NativeKit.PutInt(current, "leaf", 1, FonPutMode.Add, &error));
        for (int i = 0; i < wraps; i++) {
            IntPtr wrap = NativeBindings.fon_collection_create();
            Assert.Equal(FonResultCode.OK, NativeKit.PutCollection(wrap, "n", current, FonPutMode.Add, &error));
            current = wrap;
        }
        return current;
    }


    /// <summary>Serializes a dump holding the record as its only entry. Consumes the record.</summary>
    private static byte[] SerializeOneRecordDump(IntPtr record) {
        IntPtr dump = NativeBindings.fon_dump_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 0, record, FonPutMode.Add, &error));

            byte* buffer = null;
            long length = 0;
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_serialize_dump(dump, 0, &buffer, &length, &error));
            try {
                return new ReadOnlySpan<byte>(buffer, checked((int)length)).ToArray();
            } finally {
                NativeBindings.fon_buffer_free(buffer, length);
            }
        } finally {
            NativeBindings.fon_dump_free(dump);
        }
    }


    /// <summary>Parses text as a dump. Returns the owned dump handle, or zero with the error code.</summary>
    private static IntPtr TryParseDump(byte[] text, out int code) {
        FonError error = default;
        fixed (byte* textPtr = text) {
            IntPtr dump = NativeBindings.fon_deserialize_dump(textPtr, text.Length, 0, &error);
            code = error.Code;
            return dump;
        }
    }
}
