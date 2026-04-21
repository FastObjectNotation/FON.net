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


    [Fact]
    public async Task CrossImpl_ManagedWritesNested_NativeReads() {
        var filePath = Path.Combine(_testDir, "cross-mn.fon");
        var error = new FonError();

        var inner = new global::FON.Types.FonCollection { { "x", 17 }, { "name", "with}brace,here" } };
        var arrItems = new List<global::FON.Types.FonCollection> {
            new global::FON.Types.FonCollection { { "id", 1 } },
            new global::FON.Types.FonCollection { { "id", 2 } }
        };
        var outer = new global::FON.Types.FonCollection {
            { "wrap", inner },
            { "items", arrItems }
        };

        var dump = new global::FON.Types.FonDump();
        dump.TryAdd(0, outer);

        await global::FON.Core.Fon.SerializeToFileAutoAsync(dump, new FileInfo(filePath));

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        Assert.NotEqual(IntPtr.Zero, loaded);

        var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
        var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "wrap", ref error);
        NativeBindings.fon_collection_get_int(loadedInner, "x", out var x, ref error);
        Assert.Equal(17, x);

        var nameBuf = new byte[256];
        NativeBindings.fon_collection_get_string(loadedInner, "name", nameBuf, nameBuf.LongLength, ref error);
        Assert.Equal("with}brace,here", System.Text.Encoding.UTF8.GetString(nameBuf).TrimEnd('\0'));

        NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", null, 0, out var size, ref error);
        Assert.Equal(2, size);

        var buf = new IntPtr[2];
        NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", buf, 2, out _, ref error);
        NativeBindings.fon_collection_get_int(buf[0], "id", out var id0, ref error);
        NativeBindings.fon_collection_get_int(buf[1], "id", out var id1, ref error);
        Assert.Equal(1, id0);
        Assert.Equal(2, id1);

        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public async Task CrossImpl_NativeWritesNested_ManagedReads() {
        var filePath = Path.Combine(_testDir, "cross-nm.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var inner = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(inner, "x", 31, ref error);
        NativeBindings.fon_collection_add_string(inner, "msg", "}meta,test", ref error);
        NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);

        var c1 = NativeBindings.fon_collection_create();
        var c2 = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(c1, "id", 100, ref error);
        NativeBindings.fon_collection_add_int(c2, "id", 200, ref error);
        var children = new IntPtr[] { c1, c2 };
        NativeBindings.fon_collection_add_collection_array(outer, "items", children, 2, ref error);

        NativeBindings.fon_dump_add(dump, 0, outer, ref error);
        NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        NativeBindings.fon_dump_free(dump);

        var loaded = await global::FON.Core.Fon.DeserializeFromFileAutoAsync(new FileInfo(filePath));
        Assert.Equal(1, loaded.Count);

        var loadedOuter = loaded[0];
        var loadedInner = loadedOuter.Get<global::FON.Types.FonCollection>("wrap");
        Assert.Equal(31, loadedInner.Get<int>("x"));
        Assert.Equal("}meta,test", loadedInner.Get<string>("msg"));

        var loadedItems = loadedOuter.Get<List<global::FON.Types.FonCollection>>("items");
        Assert.Equal(2, loadedItems.Count);
        Assert.Equal(100, loadedItems[0].Get<int>("id"));
        Assert.Equal(200, loadedItems[1].Get<int>("id"));
    }
}
