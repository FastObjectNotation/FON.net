using System.Runtime.InteropServices;
using System.Text;

namespace FON.Native;


/// <summary>
/// Details of a failed call: a result code and a UTF-8 message of at most 255 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FonError {
    /// <summary>One of the <see cref="FonResultCode"/> values.</summary>
    public int Code;


    /// <summary>The message, decoded up to its first NUL byte. Assigned values longer than 255 bytes are cut at a character boundary.</summary>
    public string Message {
        get {
            fixed (byte* p = messageBytes) {
                int length = 0;
                while (length < 256 && p[length] != 0) {
                    length++;
                }
                return Encoding.UTF8.GetString(p, length);
            }
        }
        set {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            int length = Math.Min(bytes.Length, 255);
            while (length > 0 && length < bytes.Length && (bytes[length] & 0xC0) == 0x80) {
                length--;
            }
            fixed (byte* p = messageBytes) {
                for (int i = 0; i < length; i++) {
                    p[i] = bytes[i];
                }
                p[length] = 0;
            }
        }
    }


    private fixed byte messageBytes[256];
}



/// <summary>
/// Result codes returned by the native calls.
/// </summary>
public static class FonResultCode {
    public const int OK = 0;
    public const int FileNotFound = 1;
    public const int ParseFailed = 2;
    public const int WriteFailed = 3;
    public const int InvalidArgument = 4;
    public const int BufferTooSmall = 5;
    public const int NotFound = 6;
    public const int TypeMismatch = 7;
    public const int AlreadyExists = 8;
    public const int StaleView = 9;
}



/// <summary>
/// Modes of a put: the key or id must be absent, must exist, or may be either.
/// </summary>
public static class FonPutMode {
    /// <summary>The key or id must not exist.</summary>
    public const int Add = 0;

    /// <summary>The key or id must exist; the value is replaced.</summary>
    public const int Set = 1;

    /// <summary>Replace if present, add if absent.</summary>
    public const int Upsert = 2;
}



/// <summary>
/// Type characters of the native value kinds. A packed kind is the type character, or'ed with
/// <see cref="ArrayFlag"/> for arrays.
/// </summary>
public static class FonTypeCode {
    public const char Byte = 'e';
    public const char Short = 't';
    public const char Int = 'i';
    public const char UInt = 'u';
    public const char Long = 'l';
    public const char ULong = 'g';
    public const char Float = 'f';
    public const char Double = 'd';
    public const char Bool = 'b';
    public const char String = 's';
    public const char Raw = 'r';
    public const char Object = 'o';
    public const int ArrayFlag = 0x100;
}



/// <summary>
/// Direct entry points of the native library. Handles are <see cref="IntPtr"/>, strings are
/// (pointer, byte length) pairs, and every function that can fail takes a
/// <see cref="FonError"/> out-pointer.
/// </summary>
public static unsafe class NativeBindings {
    private const string LibraryName = "fon_native";



    // ==================== VERSION / CONFIGURATION / BUFFERS ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_version();


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_set_raw_unpack(int enable);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_set_max_depth(int depth);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_buffer_free(byte* ptr, long len);



    // ==================== COLLECTION: LIFECYCLE / INTROSPECTION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_collection_create();


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_collection_free(IntPtr collection);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long fon_collection_size(IntPtr collection);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_collection_clone(IntPtr collection, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_kind_of(IntPtr collection, byte* key, long keyLength);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_keys(
        IntPtr collection,
        long* lengths,
        long lengthsCapacity,
        byte* bytes,
        long bytesCapacity,
        int* kinds,
        long* count,
        long* totalBytes,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_remove(IntPtr collection, byte* key, long keyLength);



    // ==================== COLLECTION: PUT ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_byte(IntPtr collection, byte* key, long keyLength, byte value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_short(IntPtr collection, byte* key, long keyLength, short value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_int(IntPtr collection, byte* key, long keyLength, int value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_uint(IntPtr collection, byte* key, long keyLength, uint value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_long(IntPtr collection, byte* key, long keyLength, long value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_ulong(IntPtr collection, byte* key, long keyLength, ulong value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_float(IntPtr collection, byte* key, long keyLength, float value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_double(IntPtr collection, byte* key, long keyLength, double value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_bool(IntPtr collection, byte* key, long keyLength, int value, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_string(
        IntPtr collection,
        byte* key,
        long keyLength,
        byte* value,
        long valueLength,
        int mode,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_raw(
        IntPtr collection,
        byte* key,
        long keyLength,
        byte* data,
        long dataLength,
        int mode,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_byte_array(IntPtr collection, byte* key, long keyLength, byte* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_short_array(IntPtr collection, byte* key, long keyLength, short* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_int_array(IntPtr collection, byte* key, long keyLength, int* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_uint_array(IntPtr collection, byte* key, long keyLength, uint* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_long_array(IntPtr collection, byte* key, long keyLength, long* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_ulong_array(IntPtr collection, byte* key, long keyLength, ulong* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_float_array(IntPtr collection, byte* key, long keyLength, float* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_double_array(IntPtr collection, byte* key, long keyLength, double* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_bool_array(IntPtr collection, byte* key, long keyLength, byte* values, long count, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_string_array(
        IntPtr collection,
        byte* key,
        long keyLength,
        long* lengths,
        byte* bytes,
        long count,
        int mode,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_collection(IntPtr collection, byte* key, long keyLength, IntPtr child, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_put_collection_array(
        IntPtr collection,
        byte* key,
        long keyLength,
        IntPtr* children,
        long count,
        int mode,
        FonError* error
    );



    // ==================== COLLECTION: GET ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_byte(IntPtr collection, byte* key, long keyLength, byte* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_short(IntPtr collection, byte* key, long keyLength, short* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_int(IntPtr collection, byte* key, long keyLength, int* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_uint(IntPtr collection, byte* key, long keyLength, uint* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_long(IntPtr collection, byte* key, long keyLength, long* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_ulong(IntPtr collection, byte* key, long keyLength, ulong* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_float(IntPtr collection, byte* key, long keyLength, float* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_double(IntPtr collection, byte* key, long keyLength, double* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_bool(IntPtr collection, byte* key, long keyLength, int* outValue, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_string(
        IntPtr collection,
        byte* key,
        long keyLength,
        byte* buffer,
        long capacity,
        long* actual,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_raw(
        IntPtr collection,
        byte* key,
        long keyLength,
        byte* buffer,
        long capacity,
        long* actual,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_byte_array(IntPtr collection, byte* key, long keyLength, byte* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_short_array(IntPtr collection, byte* key, long keyLength, short* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_int_array(IntPtr collection, byte* key, long keyLength, int* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_uint_array(IntPtr collection, byte* key, long keyLength, uint* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_long_array(IntPtr collection, byte* key, long keyLength, long* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_ulong_array(IntPtr collection, byte* key, long keyLength, ulong* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_float_array(IntPtr collection, byte* key, long keyLength, float* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_double_array(IntPtr collection, byte* key, long keyLength, double* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_bool_array(IntPtr collection, byte* key, long keyLength, byte* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_string_array(
        IntPtr collection,
        byte* key,
        long keyLength,
        long* lengths,
        long lengthsCapacity,
        byte* bytes,
        long bytesCapacity,
        long* count,
        long* totalBytes,
        FonError* error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_collection_get_collection(IntPtr collection, byte* key, long keyLength, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_collection_get_collection_array(
        IntPtr collection,
        byte* key,
        long keyLength,
        IntPtr* buffer,
        long capacity,
        long* actual,
        FonError* error
    );



    // ==================== DUMP ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_dump_create();


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_dump_free(IntPtr dump);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long fon_dump_size(IntPtr dump);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_dump_ids(IntPtr dump, ulong* buffer, long capacity, long* actual, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_dump_get(IntPtr dump, ulong id);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_dump_put(IntPtr dump, ulong id, IntPtr collection, int mode, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_dump_remove(IntPtr dump, ulong id);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long fon_dump_remove_many(IntPtr dump, ulong* ids, long count);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_dump_gens(IntPtr dump, ulong id, uint* recordGen, uint* treeGen);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_dump_touch(IntPtr dump, ulong id);



    // ==================== SERIALIZE / DESERIALIZE ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_serialize_dump(IntPtr dump, int maxThreads, byte** outBuffer, long* outLength, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_serialize_collection(IntPtr collection, byte** outBuffer, long* outLength, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_serialize_to_file(IntPtr dump, byte* path, long pathLength, int maxThreads, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_deserialize_dump(byte* data, long length, int maxThreads, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_deserialize_collection(byte* data, long length, FonError* error);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_deserialize_from_file(byte* path, long pathLength, int maxThreads, FonError* error);
}
