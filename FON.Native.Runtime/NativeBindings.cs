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
/// P/Invoke bindings for FON native library
/// </summary>
public static class NativeBindings {
    private const string LibraryName = "fon_native";



    // ==================== VERSION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_version();



    // ==================== CONFIGURATION ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_set_raw_unpack(int enable);



    // ==================== MEMORY MANAGEMENT ====================

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_dump_create();


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fon_dump_free(IntPtr dump);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long fon_dump_size(IntPtr dump);


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fon_dump_get(IntPtr dump, ulong index);


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


    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int fon_collection_add_float_array(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        float[] values,
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
}
