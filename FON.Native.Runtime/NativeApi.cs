using System.Buffers;
using System.Text;


namespace FON.Native;


/// <summary>
/// High-level convenience wrappers around <see cref="NativeBindings"/> for in-memory
/// serialization and deserialization to/from UTF-8 strings and byte spans.
///
/// All methods throw <see cref="FonNativeException"/> on native error. Caller is responsible
/// for freeing returned <see cref="IntPtr"/> handles via <see cref="NativeBindings.fon_dump_free"/>
/// or <see cref="NativeBindings.fon_collection_free"/>.
/// </summary>
public static class NativeApi {
    private static readonly UTF8Encoding utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);



    public static string SerializeDump(IntPtr dump, int maxThreads = 0) {
        if (dump == IntPtr.Zero) {
            throw new ArgumentException("Dump handle is null", nameof(dump));
        }

        FonError error = default;
        unsafe {
            int rc = NativeBindings.fon_serialize_dump_to_buffer(dump, null, 0, out long required, maxThreads, ref error);
            ThrowIfError(rc, error);

            if (required == 0) {
                return string.Empty;
            }

            int size = checked((int)required);
            byte[] rented = ArrayPool<byte>.Shared.Rent(size);
            try {
                fixed (byte* p = rented) {
                    rc = NativeBindings.fon_serialize_dump_to_buffer(dump, p, size, out required, maxThreads, ref error);
                }
                ThrowIfError(rc, error);
                return utf8NoBom.GetString(rented, 0, size);
            } finally {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }


    public static string SerializeCollection(IntPtr collection) {
        if (collection == IntPtr.Zero) {
            throw new ArgumentException("Collection handle is null", nameof(collection));
        }

        FonError error = default;
        unsafe {
            int rc = NativeBindings.fon_serialize_collection_to_buffer(collection, null, 0, out long required, ref error);
            ThrowIfError(rc, error);

            if (required == 0) {
                return string.Empty;
            }

            int size = checked((int)required);
            byte[] rented = ArrayPool<byte>.Shared.Rent(size);
            try {
                fixed (byte* p = rented) {
                    rc = NativeBindings.fon_serialize_collection_to_buffer(collection, p, size, out required, ref error);
                }
                ThrowIfError(rc, error);
                return utf8NoBom.GetString(rented, 0, size);
            } finally {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }


    /// <summary>
    /// Hot-path overload: serializes the dump directly into a caller-provided UTF-8 span.
    /// Returns true on success; false if the destination is too small (use <paramref name="written"/>
    /// — set to required size — to retry).
    /// </summary>
    public static bool TrySerializeDump(IntPtr dump, Span<byte> destination, out int written, int maxThreads = 0) {
        if (dump == IntPtr.Zero) {
            throw new ArgumentException("Dump handle is null", nameof(dump));
        }

        FonError error = default;
        unsafe {
            fixed (byte* p = destination) {
                int rc = NativeBindings.fon_serialize_dump_to_buffer(
                    dump, p, destination.Length, out long required, maxThreads, ref error
                );
                ThrowIfError(rc, error);
                written = checked((int)required);
                return required <= destination.Length;
            }
        }
    }


    /// <summary>
    /// Hot-path overload for a single collection. See <see cref="TrySerializeDump"/>.
    /// </summary>
    public static bool TrySerializeCollection(IntPtr collection, Span<byte> destination, out int written) {
        if (collection == IntPtr.Zero) {
            throw new ArgumentException("Collection handle is null", nameof(collection));
        }

        FonError error = default;
        unsafe {
            fixed (byte* p = destination) {
                int rc = NativeBindings.fon_serialize_collection_to_buffer(
                    collection, p, destination.Length, out long required, ref error
                );
                ThrowIfError(rc, error);
                written = checked((int)required);
                return required <= destination.Length;
            }
        }
    }


    public static IntPtr DeserializeDump(ReadOnlySpan<byte> utf8, int maxThreads = 0) {
        FonError error = default;
        unsafe {
            fixed (byte* p = utf8) {
                IntPtr handle = NativeBindings.fon_deserialize_dump_from_buffer(p, utf8.Length, maxThreads, ref error);
                if (handle == IntPtr.Zero) {
                    throw new FonNativeException(error);
                }
                return handle;
            }
        }
    }


    public static IntPtr DeserializeDump(string text, int maxThreads = 0) {
        ArgumentNullException.ThrowIfNull(text);
        int byteCount = utf8NoBom.GetByteCount(text);
        byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try {
            int actual = utf8NoBom.GetBytes(text, rented);
            return DeserializeDump(rented.AsSpan(0, actual), maxThreads);
        } finally {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }


    public static IntPtr DeserializeCollection(ReadOnlySpan<byte> utf8) {
        FonError error = default;
        unsafe {
            fixed (byte* p = utf8) {
                IntPtr handle = NativeBindings.fon_deserialize_collection_from_buffer(p, utf8.Length, ref error);
                if (handle == IntPtr.Zero) {
                    throw new FonNativeException(error);
                }
                return handle;
            }
        }
    }


    public static IntPtr DeserializeCollection(string text) {
        ArgumentNullException.ThrowIfNull(text);
        int byteCount = utf8NoBom.GetByteCount(text);
        byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try {
            int actual = utf8NoBom.GetBytes(text, rented);
            return DeserializeCollection(rented.AsSpan(0, actual));
        } finally {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }



    private static void ThrowIfError(int code, FonError error) {
        if (code != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
    }
}



/// <summary>
/// Exception thrown by <see cref="NativeApi"/> when a native call returns a non-OK code.
/// </summary>
public class FonNativeException : Exception {
    public int Code { get; }


    public FonNativeException(FonError error) : base(error.Message ?? "Unknown native error") {
        Code = error.Code;
    }
}
