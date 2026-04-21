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


    [Fact]
    public void NativeRoundTrip_NestedObject_ReadsBack() {
        var filePath = Path.Combine(testDir, "rt-nested.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var inner = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(inner, "x", 99, ref error);
        NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);
        NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        NativeBindings.fon_dump_free(dump);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        Assert.NotEqual(IntPtr.Zero, loaded);

        var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
        var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "wrap", ref error);
        Assert.NotEqual(IntPtr.Zero, loadedInner);

        NativeBindings.fon_collection_get_int(loadedInner, "x", out var x, ref error);
        Assert.Equal(99, x);

        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeRoundTrip_ArrayOfObjects_ReadsBack() {
        var filePath = Path.Combine(testDir, "rt-array.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var c1 = NativeBindings.fon_collection_create();
        var c2 = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(c1, "id", 10, ref error);
        NativeBindings.fon_collection_add_int(c2, "id", 20, ref error);
        var children = new IntPtr[] { c1, c2 };
        NativeBindings.fon_collection_add_collection_array(outer, "items", children, 2, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);
        NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        NativeBindings.fon_dump_free(dump);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);

        NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", null, 0, out var actualSize, ref error);
        Assert.Equal(2, actualSize);

        var buffer = new IntPtr[2];
        NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", buffer, buffer.LongLength, out actualSize, ref error);
        Assert.Equal(2, actualSize);

        NativeBindings.fon_collection_get_int(buffer[0], "id", out var id0, ref error);
        NativeBindings.fon_collection_get_int(buffer[1], "id", out var id1, ref error);
        Assert.Equal(10, id0);
        Assert.Equal(20, id1);

        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeRoundTrip_EmptyNestedObject_ReadsBack() {
        var filePath = Path.Combine(testDir, "rt-empty.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var inner = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_collection(outer, "empty", inner, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);
        NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        NativeBindings.fon_dump_free(dump);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
        var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "empty", ref error);
        Assert.NotEqual(IntPtr.Zero, loadedInner);
        Assert.Equal(0, NativeBindings.fon_collection_size(loadedInner));

        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeMaxDepth_BeyondLimit_ParseFails() {
        var filePath = Path.Combine(testDir, "deep.fon");
        var error = new FonError();

        // Build a depth-3 nested object as raw text.
        File.WriteAllText(filePath, "a=o:{b=o:{c=o:{d=i:1}}}\n");

        NativeBindings.fon_set_max_depth(2);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        Assert.Equal(IntPtr.Zero, loaded);
        Assert.Equal(FonResultCode.ParseFailed, error.Code);

        // Reset for other tests
        NativeBindings.fon_set_max_depth(64);
    }


    [Fact]
    public void NativeMaxDepth_AtLimit_Succeeds() {
        var filePath = Path.Combine(testDir, "atlimit.fon");
        var error = new FonError();

        File.WriteAllText(filePath, "a=o:{b=o:{c=i:1}}\n");

        NativeBindings.fon_set_max_depth(2);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        Assert.NotEqual(IntPtr.Zero, loaded);

        NativeBindings.fon_set_max_depth(64);
        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeMaxDepth_NegativeValue_ClampedToOne() {
        var filePath = Path.Combine(testDir, "clamp.fon");
        var error = new FonError();

        File.WriteAllText(filePath, "a=o:{x=i:1}\n");

        NativeBindings.fon_set_max_depth(-5);

        // depth=1 is at the clamped limit; should still parse
        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        Assert.NotEqual(IntPtr.Zero, loaded);

        NativeBindings.fon_set_max_depth(64);
        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeRoundTrip_EmptyObjectArray_ReadsBack() {
        var filePath = Path.Combine(testDir, "rt-empty-array.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_collection_array(outer, "items", Array.Empty<IntPtr>(), 0, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);
        NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        NativeBindings.fon_dump_free(dump);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
        NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", null, 0, out var size, ref error);
        Assert.Equal(0, size);

        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeRoundTrip_NestedStringWithBraces_ReadsBack() {
        var filePath = Path.Combine(testDir, "rt-meta.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var inner = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_string(inner, "txt", "has}brace,and{open[bracket]", ref error);
        NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);
        NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        NativeBindings.fon_dump_free(dump);

        var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
        var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
        var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "wrap", ref error);

        var buffer = new byte[256];
        NativeBindings.fon_collection_get_string(loadedInner, "txt", buffer, buffer.LongLength, ref error);
        var got = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        Assert.Equal("has}brace,and{open[bracket]", got);

        NativeBindings.fon_dump_free(loaded);
    }


    [Fact]
    public void NativeAdd_ChildHandleInvalidatedAfterAdd() {
        // Documents the ownership contract from the spec: once add_collection succeeds,
        // the child handle is owned by the parent. Calling fon_collection_free on it
        // afterward would be a double-free, so this test demonstrates the correct usage
        // pattern: do NOT free child after add.
        var error = new FonError();
        var parent = NativeBindings.fon_collection_create();
        var child = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(child, "x", 1, ref error);

        var rc = NativeBindings.fon_collection_add_collection(parent, "c", child, ref error);
        Assert.Equal(FonResultCode.OK, rc);

        // child is now invalidated — only free the parent.
        NativeBindings.fon_collection_free(parent);
    }
}
