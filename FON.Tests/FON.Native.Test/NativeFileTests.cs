using System.Text;
using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Raw ABI tests for file and in-memory serialization, owned buffers and parse errors.
/// </summary>
public unsafe class NativeFileTests : IDisposable {
    private readonly string testDir;

    public NativeFileTests() {
        testDir = Path.Combine(Path.GetTempPath(), $"fon_native_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
    }


    public void Dispose() {
        if (Directory.Exists(testDir)) {
            Directory.Delete(testDir, recursive: true);
        }
    }


    [Fact]
    public void NativeSerializeToFile_ThenDeserializeFromFile_RoundTrips() {
        string filePath = Path.Combine(testDir, "test.fon");
        FonError error = default;
        IntPtr dump = NativeBindings.fon_dump_create();
        IntPtr first = NativeBindings.fon_collection_create();
        IntPtr second = NativeBindings.fon_collection_create();
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(first, "id", 123, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(first, "name", "Test", FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(second, "id", 456, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 0, first, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 1, second, FonPutMode.Add, &error));

            byte[] path = NativeKit.Utf8(filePath);
            fixed (byte* pathPtr = path) {
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_serialize_to_file(dump, pathPtr, path.Length, 0, &error));
            }
        } finally {
            NativeBindings.fon_dump_free(dump);
        }

        byte[] readPath = NativeKit.Utf8(filePath);
        IntPtr loaded;
        fixed (byte* pathPtr = readPath) {
            loaded = NativeBindings.fon_deserialize_from_file(pathPtr, readPath.Length, 0, &error);
        }
        Assert.NotEqual(IntPtr.Zero, loaded);
        try {
            Assert.Equal(2, NativeBindings.fon_dump_size(loaded));
            IntPtr record = NativeBindings.fon_dump_get(loaded, 1);
            Assert.NotEqual(IntPtr.Zero, record);
            Assert.Equal(FonResultCode.OK, NativeKit.GetInt(record, "id", out int value, &error));
            Assert.Equal(456, value);
        } finally {
            NativeBindings.fon_dump_free(loaded);
        }
    }


    [Fact]
    public void NativeDeserializeFromFile_MissingFile_ReturnsFileNotFound() {
        FonError error = default;
        byte[] path = NativeKit.Utf8(Path.Combine(testDir, "absent.fon"));
        IntPtr handle;
        fixed (byte* pathPtr = path) {
            handle = NativeBindings.fon_deserialize_from_file(pathPtr, path.Length, 0, &error);
        }
        Assert.Equal(IntPtr.Zero, handle);
        Assert.Equal(FonResultCode.FileNotFound, error.Code);
    }


    [Fact]
    public void NativeSerializeDump_OwnedBuffer_ReleasedWithBufferFree() {
        FonError error = default;
        IntPtr dump = NativeBindings.fon_dump_create();
        IntPtr record = NativeBindings.fon_collection_create();
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(record, "s", "hello", FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 0, record, FonPutMode.Add, &error));

            byte* buffer = null;
            long length = 0;
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_serialize_dump(dump, 0, &buffer, &length, &error));
            try {
                string text = Encoding.UTF8.GetString(buffer, checked((int)length));
                Assert.Contains("hello", text);
            } finally {
                NativeBindings.fon_buffer_free(buffer, length);
            }
        } finally {
            NativeBindings.fon_dump_free(dump);
        }
    }


    [Fact]
    public void NativeSerializeCollection_ThenDeserializeCollection_RoundTrips() {
        FonError error = default;
        IntPtr collection = NativeBindings.fon_collection_create();
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "n", 5, FonPutMode.Add, &error));
            byte* buffer = null;
            long length = 0;
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_serialize_collection(collection, &buffer, &length, &error));
            IntPtr loaded;
            try {
                loaded = NativeBindings.fon_deserialize_collection(buffer, length, &error);
            } finally {
                NativeBindings.fon_buffer_free(buffer, length);
            }
            Assert.NotEqual(IntPtr.Zero, loaded);
            try {
                Assert.Equal(FonResultCode.OK, NativeKit.GetInt(loaded, "n", out int value, &error));
                Assert.Equal(5, value);
            } finally {
                NativeBindings.fon_collection_free(loaded);
            }
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeDeserializeDump_InvalidInput_ReturnsParseFailed() {
        FonError error = default;
        byte[] text = NativeKit.Utf8("a=i:not-a-number\n");
        IntPtr handle;
        fixed (byte* textPtr = text) {
            handle = NativeBindings.fon_deserialize_dump(textPtr, text.Length, 0, &error);
        }
        Assert.Equal(IntPtr.Zero, handle);
        Assert.Equal(FonResultCode.ParseFailed, error.Code);
        Assert.False(string.IsNullOrEmpty(error.Message));
    }
}
