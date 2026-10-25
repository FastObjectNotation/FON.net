using System.Text;
using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Raw ABI tests for the size-query and multi-string patterns of fon.h.
/// </summary>
public unsafe class NativeBufferTests {
    [Fact]
    public void NativeVersion_IsNulTerminatedSemver() {
        string version = ReadUtf8(NativeBindings.fon_version());
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }


    [Fact]
    public void NativeString_SizeQueryWithNullBuffer_ReportsExactByteCount() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        string value = new string('a', 8192);
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "s", value, FonPutMode.Add, &error));
            byte[] key = NativeKit.Utf8("s");
            long actual = -1;
            fixed (byte* keyPtr = key) {
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_string(collection, keyPtr, key.Length, null, 0, &actual, &error));
            }
            Assert.Equal(8192L, actual);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeString_CapacityOneTooSmall_ReturnsBufferTooSmallAndWritesNothing() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "s", "abcdef", FonPutMode.Add, &error));
            byte[] key = NativeKit.Utf8("s");
            byte[] buffer = new byte[6];
            Array.Fill(buffer, (byte)0xAA);
            long actual = 0;
            fixed (byte* keyPtr = key) {
                fixed (byte* bufferPtr = buffer) {
                    int rc = NativeBindings.fon_collection_get_string(collection, keyPtr, key.Length, bufferPtr, 5, &actual, &error);
                    Assert.Equal(FonResultCode.BufferTooSmall, rc);
                }
            }
            Assert.Equal(6L, actual);
            Assert.All(buffer, (byte b) => Assert.Equal((byte)0xAA, b));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeString_HundredThousandBytes_RoundTripsIntact() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        string value = new string('x', 100_000);
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "big", value, FonPutMode.Add, &error));
            Assert.Equal(value, NativeKit.GetString(collection, "big"));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeString_EmbeddedNul_InValueIsPreserved() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            string value = "a\0b";
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "v", value, FonPutMode.Add, &error));
            Assert.Equal(value, NativeKit.GetString(collection, "v"));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeKeys_EmbeddedNulAndLongKey_RoundTrip() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        string longKey = new string('k', 600);
        string nulKey = "x\0y";
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, nulKey, 1, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, longKey, 2, FonPutMode.Add, &error));

            List<(string Key, int Kind)> keys = NativeKit.KeysWithKinds(collection);

            Assert.Equal(nulKey, keys[0].Key);
            Assert.Equal(longKey, keys[1].Key);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeStringArray_MultiStringPattern_RoundTripsWithEmptyItems() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        string[] items = { "x", string.Empty, new string('b', 8192), "y" };
        byte[] itemBytes = items.SelectMany(item => Encoding.UTF8.GetBytes(item)).ToArray();
        long[] lengths = items.Select(item => (long)Encoding.UTF8.GetByteCount(item)).ToArray();
        byte[] key = NativeKit.Utf8("arr");
        try {
            fixed (byte* keyPtr = key) {
                fixed (long* lengthPtr = lengths) {
                    fixed (byte* bytePtr = itemBytes) {
                        int rc = NativeBindings.fon_collection_put_string_array(collection, keyPtr, key.Length, lengthPtr, bytePtr, items.Length, FonPutMode.Add, &error);
                        Assert.Equal(FonResultCode.OK, rc);
                    }
                }
            }

            long count = 0;
            long totalBytes = 0;
            fixed (byte* keyPtr = key) {
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_string_array(collection, keyPtr, key.Length, null, 0, null, 0, &count, &totalBytes, &error));
            }
            Assert.Equal((long)items.Length, count);
            Assert.Equal((long)itemBytes.Length, totalBytes);

            long[] readLengths = new long[count];
            byte[] readBytes = new byte[totalBytes];
            fixed (byte* keyPtr = key) {
                fixed (long* lengthPtr = readLengths) {
                    fixed (byte* bytePtr = readBytes) {
                        Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_string_array(collection, keyPtr, key.Length, lengthPtr, count, bytePtr, totalBytes, &count, &totalBytes, &error));
                    }
                }
            }

            Assert.Equal("x", Encoding.UTF8.GetString(readBytes, 0, (int)readLengths[0]));
            Assert.Equal(0L, readLengths[1]);
            Assert.Equal(items[2], Encoding.UTF8.GetString(readBytes, 1, (int)readLengths[2]));
            Assert.Equal("y", Encoding.UTF8.GetString(readBytes, 1 + 8192, (int)readLengths[3]));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeIntArray_SizeQueryThenFill() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        int[] values = { int.MinValue, -1, 0, 1, int.MaxValue };
        byte[] key = NativeKit.Utf8("ints");
        try {
            fixed (byte* keyPtr = key) {
                fixed (int* valuePtr = values) {
                    Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_put_int_array(collection, keyPtr, key.Length, valuePtr, values.Length, FonPutMode.Add, &error));
                }
            }

            long actual = 0;
            fixed (byte* keyPtr = key) {
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_int_array(collection, keyPtr, key.Length, null, 0, &actual, &error));
            }
            Assert.Equal((long)values.Length, actual);

            int[] read = new int[actual];
            fixed (byte* keyPtr = key) {
                fixed (int* readPtr = read) {
                    Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_int_array(collection, keyPtr, key.Length, readPtr, actual, &actual, &error));
                }
            }
            Assert.Equal(values, read);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    private static string ReadUtf8(IntPtr pointer) {
        return System.Runtime.InteropServices.Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
    }
}
