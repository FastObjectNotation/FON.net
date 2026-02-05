using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Tests for native file serialization/deserialization.
/// </summary>
public class NativeFileTests : IDisposable {
    private readonly string _testDir;

    public NativeFileTests() {
        _testDir = Path.Combine(Path.GetTempPath(), $"fon_native_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose() {
        if (Directory.Exists(_testDir)) {
            Directory.Delete(_testDir, recursive: true);
        }
    }


    [Fact]
    public void NativeSerialize_AndDeserialize_RoundTrips() {
        var filePath = Path.Combine(_testDir, "test.fon");
        var error = new FonError();

        // Create and populate dump
        var dump = NativeBindings.fon_dump_create();
        var collection = NativeBindings.fon_collection_create();

        NativeBindings.fon_collection_add_int(collection, "id", 123, ref error);
        NativeBindings.fon_collection_add_string(collection, "name", "Test", ref error);
        NativeBindings.fon_collection_add_float(collection, "value", 45.67f, ref error);

        NativeBindings.fon_dump_add(dump, 0, collection, ref error);

        // Serialize
        var result = NativeBindings.fon_serialize_to_file(dump, filePath, Environment.ProcessorCount, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.True(File.Exists(filePath));

        NativeBindings.fon_dump_free(dump);

        // Deserialize
        var loadedDump = NativeBindings.fon_deserialize_from_file(filePath, Environment.ProcessorCount, ref error);
        Assert.NotEqual(IntPtr.Zero, loadedDump);

        var size = NativeBindings.fon_dump_size(loadedDump);
        Assert.Equal(1, size);

        var loadedCollection = NativeBindings.fon_dump_get(loadedDump, 0);
        Assert.NotEqual(IntPtr.Zero, loadedCollection);

        // Verify values
        NativeBindings.fon_collection_get_int(loadedCollection, "id", out int id, ref error);
        Assert.Equal(123, id);

        var nameBuffer = new byte[256];
        NativeBindings.fon_collection_get_string(loadedCollection, "name", nameBuffer, nameBuffer.Length, ref error);
        var name = System.Text.Encoding.UTF8.GetString(nameBuffer).TrimEnd('\0');
        Assert.Equal("Test", name);

        NativeBindings.fon_collection_get_float(loadedCollection, "value", out float value, ref error);
        Assert.Equal(45.67f, value, precision: 2);

        NativeBindings.fon_dump_free(loadedDump);
    }


    [Fact]
    public void NativeSerialize_LargeData_Works() {
        var filePath = Path.Combine(_testDir, "large.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();

        // Add 1000 collections
        for (ulong i = 0; i < 1000; i++) {
            var collection = NativeBindings.fon_collection_create();
            NativeBindings.fon_collection_add_long(collection, "index", (long)i, ref error);
            NativeBindings.fon_collection_add_string(collection, "data", $"Item_{i}", ref error);
            NativeBindings.fon_dump_add(dump, i, collection, ref error);
        }

        // Serialize
        var result = NativeBindings.fon_serialize_to_file(dump, filePath, Environment.ProcessorCount, ref error);
        Assert.Equal(FonResultCode.OK, result);

        NativeBindings.fon_dump_free(dump);

        // Deserialize and verify
        var loadedDump = NativeBindings.fon_deserialize_from_file(filePath, Environment.ProcessorCount, ref error);
        Assert.NotEqual(IntPtr.Zero, loadedDump);

        var size = NativeBindings.fon_dump_size(loadedDump);
        Assert.Equal(1000, size);

        NativeBindings.fon_dump_free(loadedDump);
    }
}
