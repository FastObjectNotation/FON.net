using FON.Native;
using Xunit;

namespace FON.Native.Test;


public class NativeNestedTests : IDisposable {
    private readonly string testDir;

    public NativeNestedTests() {
        testDir = Path.Combine(Path.GetTempPath(), $"fon_native_nested_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
    }


    public void Dispose() {
        if (Directory.Exists(testDir)) {
            Directory.Delete(testDir, recursive: true);
        }
    }


    [Fact]
    public void NativeSerialize_NestedObject_WritesObjectLiteral() {
        var filePath = Path.Combine(testDir, "nested.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var inner = NativeBindings.fon_collection_create();

        NativeBindings.fon_collection_add_int(inner, "x", 7, ref error);
        NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);

        var result = NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        Assert.Equal(FonResultCode.OK, result);

        var text = File.ReadAllText(filePath).TrimEnd();
        Assert.Equal("wrap=o:{x=i:7}", text);

        NativeBindings.fon_dump_free(dump);
    }


    [Fact]
    public void NativeSerialize_ArrayOfObjects_WritesArrayLiteral() {
        var filePath = Path.Combine(testDir, "array.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();

        var c1 = NativeBindings.fon_collection_create();
        var c2 = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(c1, "id", 1, ref error);
        NativeBindings.fon_collection_add_int(c2, "id", 2, ref error);

        var children = new IntPtr[] { c1, c2 };
        NativeBindings.fon_collection_add_collection_array(outer, "items", children, children.LongLength, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);

        var result = NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        Assert.Equal(FonResultCode.OK, result);

        var text = File.ReadAllText(filePath).TrimEnd();
        Assert.Equal("items=o:[{id=i:1},{id=i:2}]", text);

        NativeBindings.fon_dump_free(dump);
    }
}
