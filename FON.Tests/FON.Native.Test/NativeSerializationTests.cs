using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Tests for native serialization/deserialization operations.
/// </summary>
public class NativeSerializationTests {
    [Fact]
    public void NativeCollection_CreateAndFree() {
        var collection = NativeBindings.fon_collection_create();
        Assert.NotEqual(IntPtr.Zero, collection);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeDump_CreateAndFree() {
        var dump = NativeBindings.fon_dump_create();
        Assert.NotEqual(IntPtr.Zero, dump);

        NativeBindings.fon_dump_free(dump);
    }


    [Fact]
    public void NativeCollection_AddAndGetInt() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        var result = NativeBindings.fon_collection_add_int(collection, "test_key", 42, ref error);
        Assert.Equal(FonResultCode.OK, result);

        result = NativeBindings.fon_collection_get_int(collection, "test_key", out int value, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(42, value);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetLong() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        var result = NativeBindings.fon_collection_add_long(collection, "big_number", 9876543210L, ref error);
        Assert.Equal(FonResultCode.OK, result);

        result = NativeBindings.fon_collection_get_long(collection, "big_number", out long value, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(9876543210L, value);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetFloat() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        var result = NativeBindings.fon_collection_add_float(collection, "pi", 3.14159f, ref error);
        Assert.Equal(FonResultCode.OK, result);

        result = NativeBindings.fon_collection_get_float(collection, "pi", out float value, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(3.14159f, value, precision: 5);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetDouble() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        var result = NativeBindings.fon_collection_add_double(collection, "e", 2.71828182845904, ref error);
        Assert.Equal(FonResultCode.OK, result);

        result = NativeBindings.fon_collection_get_double(collection, "e", out double value, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(2.71828182845904, value, precision: 10);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetBool() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        var result = NativeBindings.fon_collection_add_bool(collection, "flag", 1, ref error);
        Assert.Equal(FonResultCode.OK, result);

        result = NativeBindings.fon_collection_get_bool(collection, "flag", out int value, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(1, value);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetString() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        var result = NativeBindings.fon_collection_add_string(collection, "greeting", "Hello, World!", ref error);
        Assert.Equal(FonResultCode.OK, result);

        var buffer = new byte[256];
        result = NativeBindings.fon_collection_get_string(collection, "greeting", buffer, buffer.Length, ref error);
        Assert.Equal(FonResultCode.OK, result);

        var value = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        Assert.Equal("Hello, World!", value);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetIntArray() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        int[] input = [1, 2, 3, 4, 5];
        var result = NativeBindings.fon_collection_add_int_array(collection, "numbers", input, input.Length, ref error);
        Assert.Equal(FonResultCode.OK, result);

        // First call to get size
        result = NativeBindings.fon_collection_get_int_array(collection, "numbers", null, 0, out long actualSize, ref error);
        Assert.Equal(5, actualSize);

        // Second call to get values
        var output = new int[actualSize];
        result = NativeBindings.fon_collection_get_int_array(collection, "numbers", output, output.Length, out _, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(input, output);

        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetFloatArray() {
        var collection = NativeBindings.fon_collection_create();
        var error = new FonError();

        float[] input = [1.1f, 2.2f, 3.3f];
        var result = NativeBindings.fon_collection_add_float_array(collection, "floats", input, input.Length, ref error);
        Assert.Equal(FonResultCode.OK, result);

        var output = new float[3];
        result = NativeBindings.fon_collection_get_float_array(collection, "floats", output, output.Length, out long actualSize, ref error);
        Assert.Equal(FonResultCode.OK, result);
        Assert.Equal(3, actualSize);

        for (int i = 0; i < input.Length; i++) {
            Assert.Equal(input[i], output[i], precision: 5);
        }

        NativeBindings.fon_collection_free(collection);
    }
}
