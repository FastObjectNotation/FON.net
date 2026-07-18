using System.Runtime.InteropServices;

namespace FON.Native;


/// <summary>
/// Error structure returned by native library
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct FonError {
    public int Code;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Message;
}



/// <summary>
/// Result codes from native library
/// </summary>
public static class FonResultCode {
    public const int OK = 0;
    public const int FileNotFound = 1;
    public const int ParseFailed = 2;
    public const int WriteFailed = 3;
    public const int InvalidArgument = 4;
}



/// <summary>
/// Low-level native interop for the FON acceleration library.
/// </summary>
public static class NativeBindings {
    private const string LibraryName = "fon_native";



    // ==================== VERSION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_version();



    // ==================== CONFIGURATION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_set_raw_unpack(int enable);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_set_max_depth(int depth);



    // ==================== MEMORY MANAGEMENT ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_dump_create();


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_dump_free(IntPtr dump);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long fon_dump_size(IntPtr dump);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_dump_get(IntPtr dump, ulong index);


    /// <summary>
    /// Gets the record id stored at iteration position <paramref name="position"/> (0-based,
    /// <c>0..fon_dump_size(dump)</c>). Combine with <see cref="fon_dump_get"/> to enumerate a
    /// dump without assuming ids are contiguous. Returns a non-zero code if out of range.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fon_dump_id_at(IntPtr dump, long position, out ulong id);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_collection_create();


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_collection_free(IntPtr collection);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long fon_collection_size(IntPtr collection);



    // ==================== SERIALIZATION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_serialize_to_file(
        IntPtr dump,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int maxThreads,
        ref FonError error
    );



    // ==================== DESERIALIZATION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr fon_deserialize_from_file(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int maxThreads,
        ref FonError error
    );



    // ==================== STRING / BUFFER SERIALIZATION ====================

    /// <summary>
    /// Serializes a Dump into the caller-supplied UTF-8 buffer using a two-call pattern.
    /// First call with <paramref name="buffer"/>=null and <paramref name="bufferSize"/>=0 fills
    /// <paramref name="requiredSize"/> with the exact byte count; second call with a buffer of that
    /// size receives the bytes. Output is NOT null-terminated.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int fon_serialize_dump_to_buffer(
        IntPtr dump,
        byte* buffer,
        long bufferSize,
        out long requiredSize,
        int maxThreads,
        ref FonError error
    );


    /// <summary>
    /// Serializes a single Collection into the caller-supplied UTF-8 buffer (two-call pattern).
    /// See <see cref="fon_serialize_dump_to_buffer"/> for protocol details.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int fon_serialize_collection_to_buffer(
        IntPtr collection,
        byte* buffer,
        long bufferSize,
        out long requiredSize,
        ref FonError error
    );



    // ==================== STRING / BUFFER DESERIALIZATION ====================

    /// <summary>
    /// Parses a multi-line UTF-8 buffer into a new Dump. Caller owns the returned handle and must
    /// free it via <see cref="fon_dump_free"/>. The input does not need to be null-terminated.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe IntPtr fon_deserialize_dump_from_buffer(
        byte* data,
        long size,
        int maxThreads,
        ref FonError error
    );


    /// <summary>
    /// Parses a single-line UTF-8 buffer into a new Collection. Caller owns the returned handle and
    /// must free it via <see cref="fon_collection_free"/> (unless ownership is transferred via
    /// <see cref="fon_dump_add"/> or <see cref="fon_collection_add_collection"/>).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe IntPtr fon_deserialize_collection_from_buffer(
        byte* data,
        long size,
        ref FonError error
    );



    // ==================== COLLECTION ADD OPERATIONS ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_dump_add(
        IntPtr dump,
        ulong id,
        IntPtr collection,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_int(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        int value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_long(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        long value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_float(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        float value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_double(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        double value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_bool(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        int value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_string(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_int_array(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        int[] values,
        long count,
        ref FonError error
    );


    /// <summary>
    /// Adds a nested collection under <paramref name="key"/> inside <paramref name="parent"/>.
    /// Ownership transfer: after a successful call, <paramref name="child"/> is owned by
    /// <paramref name="parent"/>. The caller MUST NOT use the child handle again and MUST NOT
    /// call <c>fon_collection_free</c> on it; doing so causes a double-free.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_collection(
        IntPtr parent,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        IntPtr child,
        ref FonError error
    );


    /// <summary>
    /// Adds an array of nested collections under <paramref name="key"/> inside <paramref name="parent"/>.
    /// Ownership transfer: after a successful call, every handle in <paramref name="children"/> is
    /// owned by <paramref name="parent"/>. The caller MUST NOT use any child handle again and MUST NOT
    /// call <c>fon_collection_free</c> on any of them; doing so causes a double-free.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_collection_array(
        IntPtr parent,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        IntPtr[] children,
        long count,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_float_array(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        float[] values,
        long count,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_byte(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte value, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_short(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, short value, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_uint(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint value, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_ulong(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, ulong value, ref FonError error);


    /// <summary>Adds raw binary data (copied) under <paramref name="key"/>.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern unsafe int fon_collection_add_raw(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte* data, long len, ref FonError error);


    // Fixed-width array add operations. Each copies `values[0..count]` into a new native array value.
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_byte_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte[] values, long count, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_short_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, short[] values, long count, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_uint_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint[] values, long count, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_long_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, long[] values, long count, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_ulong_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, ulong[] values, long count, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_double_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, double[] values, long count, ref FonError error);


    /// <summary>bool[] is marshalled as a byte buffer (0/1).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_bool_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte[] values, long count, ref FonError error);


    /// <summary>
    /// <paramref name="values"/> is an array of pointers to nul-terminated UTF-8 buffers
    /// (the marshaler cannot convert <c>string[]</c> directly to <c>LPUTF8Str[]</c>); the
    /// caller is responsible for allocating/freeing those buffers, e.g. via
    /// <see cref="System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8"/>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_string_array(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        IntPtr[] values,
        long count,
        ref FonError error
    );



    // ==================== COLLECTION GET OPERATIONS ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_int(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        out int value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_long(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        out long value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_float(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        out float value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_double(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        out double value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_bool(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        out int value,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_string(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [Out] byte[] buffer,
        long bufferSize,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_int_array(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [Out] int[]? buffer,
        long bufferSize,
        out long actualSize,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_float_array(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [Out] float[]? buffer,
        long bufferSize,
        out long actualSize,
        ref FonError error
    );


    /// <summary>
    /// Gets a borrowed handle to a nested collection under <paramref name="key"/>.
    /// Ownership: the returned handle is owned by <paramref name="parent"/>. The caller
    /// MUST NOT call <c>fon_collection_free</c> on it. Returns <see cref="IntPtr.Zero"/>
    /// if the key is missing or the value is not a nested collection.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr fon_collection_get_collection(
        IntPtr parent,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        ref FonError error
    );


    /// <summary>
    /// Gets borrowed handles for an array of nested collections. Pass <c>buffer=null</c>
    /// with <c>bufferSize=0</c> to query <paramref name="actualSize"/> only.
    /// Ownership: every returned handle is owned by <paramref name="parent"/>. The caller
    /// MUST NOT call <c>fon_collection_free</c> on any of them.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_collection_array(
        IntPtr parent,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [Out] IntPtr[]? buffer,
        long bufferSize,
        out long actualSize,
        ref FonError error
    );


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_byte(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, out byte value, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_short(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, out short value, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_uint(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, out uint value, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_ulong(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, out ulong value, ref FonError error);


    /// <summary>Reads raw binary data (two-call pattern, see <see cref="fon_collection_get_int_array"/>).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_raw(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] byte[]? buffer, long bufferSize, out long actualSize, ref FonError error);


    // Fixed-width array get operations (two-call pattern, see fon_collection_get_int_array).
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_byte_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] byte[]? buffer, long bufferSize, out long actualSize, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_short_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] short[]? buffer, long bufferSize, out long actualSize, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_uint_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] uint[]? buffer, long bufferSize, out long actualSize, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_long_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] long[]? buffer, long bufferSize, out long actualSize, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_ulong_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] ulong[]? buffer, long bufferSize, out long actualSize, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_double_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] double[]? buffer, long bufferSize, out long actualSize, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_bool_array(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [Out] byte[]? buffer, long bufferSize, out long actualSize, ref FonError error);


    // string[] elements vary in length: query the count, then fetch each item by index
    // into a nul-terminated buffer (same convention as fon_collection_get_string).
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_string_array_count(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, out long count, ref FonError error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_get_string_array_item(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, long index, [Out] byte[] buffer, long bufferSize, ref FonError error);



    // ==================== ENUMERATION / TYPE INTROSPECTION ====================

    /// <summary>Gets the key name at iteration position <paramref name="index"/> (0-based, <c>0..fon_collection_size</c>).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_key_at(IntPtr collection, long index, [Out] byte[] buffer, long bufferSize, ref FonError error);

    /// <summary>
    /// Gets the packed type tag of the value under <paramref name="key"/>: low byte is the
    /// TYPE_* char (e.g. 'i', 's', 'o'), bit 0x100 is set if the value is an array of that type.
    /// Returns -1 if the key is missing.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_kind_of(IntPtr collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);
}
