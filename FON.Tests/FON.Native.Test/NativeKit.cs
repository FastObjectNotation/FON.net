using System.Text;
using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Call helpers for the raw fon.h ABI tests. Keys and values are passed as UTF-8 byte
/// pointers with explicit lengths, exactly as the native library expects.
/// </summary>
internal static unsafe class NativeKit {
    internal static byte[] Utf8(string text) {
        return Encoding.UTF8.GetBytes(text);
    }


    internal static int PutInt(IntPtr collection, string key, int value, int mode, FonError* error) {
        byte[] keyBytes = Utf8(key);
        fixed (byte* keyPtr = keyBytes) {
            return NativeBindings.fon_collection_put_int(collection, keyPtr, keyBytes.Length, value, mode, error);
        }
    }


    internal static int PutString(IntPtr collection, string key, string value, int mode, FonError* error) {
        byte[] keyBytes = Utf8(key);
        byte[] valueBytes = Utf8(value);
        fixed (byte* keyPtr = keyBytes) {
            fixed (byte* valuePtr = valueBytes) {
                return NativeBindings.fon_collection_put_string(collection, keyPtr, keyBytes.Length, valuePtr, valueBytes.Length, mode, error);
            }
        }
    }


    internal static int PutCollection(IntPtr collection, string key, IntPtr child, int mode, FonError* error) {
        byte[] keyBytes = Utf8(key);
        fixed (byte* keyPtr = keyBytes) {
            return NativeBindings.fon_collection_put_collection(collection, keyPtr, keyBytes.Length, child, mode, error);
        }
    }


    internal static int GetInt(IntPtr collection, string key, out int value, FonError* error) {
        byte[] keyBytes = Utf8(key);
        int result = 0;
        fixed (byte* keyPtr = keyBytes) {
            int rc = NativeBindings.fon_collection_get_int(collection, keyPtr, keyBytes.Length, &result, error);
            value = result;
            return rc;
        }
    }


    /// <summary>Reads a string value with the size-query pattern (two calls).</summary>
    internal static string GetString(IntPtr collection, string key) {
        byte[] keyBytes = Utf8(key);
        FonError error = default;
        long actual = 0;
        fixed (byte* keyPtr = keyBytes) {
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_string(collection, keyPtr, keyBytes.Length, null, 0, &actual, &error));
        }

        byte[] buffer = new byte[actual];
        fixed (byte* keyPtr = keyBytes) {
            fixed (byte* bufferPtr = buffer) {
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_get_string(collection, keyPtr, keyBytes.Length, bufferPtr, actual, &actual, &error));
            }
        }
        return Encoding.UTF8.GetString(buffer);
    }


    /// <summary>Keys and packed kinds in insertion order (multi-string pattern, two calls).</summary>
    internal static List<(string Key, int Kind)> KeysWithKinds(IntPtr collection) {
        FonError error = default;
        long count = 0;
        long totalBytes = 0;
        Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_keys(collection, null, 0, null, 0, null, &count, &totalBytes, &error));

        long[] lengths = new long[count];
        byte[] bytes = new byte[totalBytes];
        int[] kinds = new int[count];
        fixed (long* lengthPtr = lengths) {
            fixed (byte* bytePtr = bytes) {
                fixed (int* kindPtr = kinds) {
                    Assert.Equal(FonResultCode.OK, NativeBindings.fon_collection_keys(collection, lengthPtr, count, bytePtr, totalBytes, kindPtr, &count, &totalBytes, &error));
                }
            }
        }

        List<(string Key, int Kind)> result = new List<(string Key, int Kind)>();
        int offset = 0;
        for (int i = 0; i < count; i++) {
            int length = (int)lengths[i];
            result.Add((Encoding.UTF8.GetString(bytes, offset, length), kinds[i]));
            offset += length;
        }
        return result;
    }


    internal static long DumpSize(IntPtr dump) {
        return NativeBindings.fon_dump_size(dump);
    }
}
