using System.Text;

namespace FON.Native;


/// <summary>
/// One key of a collection with the packed kind of its value (see <see cref="FonTypeCode"/>).
/// </summary>
internal readonly record struct KeyEntry(string Key, int Kind);



// Delegate shapes of the native families; the managed wrappers pass method groups.
internal unsafe delegate int ScalarPut<T>(IntPtr collection, byte* key, long keyLength, T value, int mode, FonError* error) where T : unmanaged;

internal unsafe delegate int BytesPut(IntPtr collection, byte* key, long keyLength, byte* value, long valueLength, int mode, FonError* error);

internal unsafe delegate int ArrayPut<T>(IntPtr collection, byte* key, long keyLength, T* values, long count, int mode, FonError* error) where T : unmanaged;

internal unsafe delegate int ScalarGet<T>(IntPtr collection, byte* key, long keyLength, T* value, FonError* error) where T : unmanaged;

internal unsafe delegate int BytesGet(IntPtr collection, byte* key, long keyLength, byte* buffer, long capacity, long* actual, FonError* error);

internal unsafe delegate int ArrayGet<T>(IntPtr collection, byte* key, long keyLength, T* buffer, long capacity, long* actual, FonError* error) where T : unmanaged;



/// <summary>
/// Call patterns over <see cref="NativeBindings"/>: UTF-8 keys, size-query and multi-string
/// reads, mutations, serialization and error mapping. Callers hold a valid native handle for
/// the duration of each call.
/// </summary>
internal static unsafe class NativeApi {
    private static readonly UTF8Encoding utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);



    // ==================== TEXT ====================

    internal static byte[] Encode(string text) {
        return utf8NoBom.GetBytes(text);
    }


    internal static string DecodeUtf8(byte[] bytes) {
        return utf8NoBom.GetString(bytes);
    }


    private static string DecodeUtf8(byte* data, long length) {
        return length == 0 ? string.Empty : utf8NoBom.GetString(data, checked((int)length));
    }



    // ==================== ERRORS ====================

    internal static void Check(int code, FonError* error) {
        if (code != FonResultCode.OK) {
            throw CreateException(code, error);
        }
    }


    /// <summary>
    /// Maps a native failure to the exception type the public API documents for it.
    /// </summary>
    internal static Exception CreateException(int code, FonError* error) {
        string message = error->Message;
        return code switch {
            FonResultCode.NotFound => new KeyNotFoundException(message),
            FonResultCode.TypeMismatch => new InvalidCastException(message),
            FonResultCode.AlreadyExists => new InvalidOperationException(message),
            FonResultCode.InvalidArgument => new ArgumentException(message),
            FonResultCode.StaleView => new FonStaleViewException(message),
            _ => new FonNativeException(code, message)
        };
    }


    internal static FonStaleViewException StaleView() {
        return new FonStaleViewException("stale view: the data this view refers to was changed or removed");
    }



    // ==================== COLLECTION: KEYS AND KINDS ====================

    /// <summary>All keys in insertion order with their kinds, in one native call.</summary>
    internal static List<KeyEntry> ReadKeys(IntPtr collection) {
        FonError error = default;
        long count = 0;
        long totalBytes = 0;
        int rc = NativeBindings.fon_collection_keys(collection, null, 0, null, 0, null, &count, &totalBytes, &error);
        Check(rc, &error);
        if (count == 0) {
            return new List<KeyEntry>();
        }

        long[] lengths = new long[count];
        byte[] bytes = new byte[totalBytes];
        int[] kinds = new int[count];
        fixed (long* lengthPtr = lengths) {
            fixed (byte* bytePtr = bytes) {
                fixed (int* kindPtr = kinds) {
                    rc = NativeBindings.fon_collection_keys(collection, lengthPtr, count, bytePtr, totalBytes, kindPtr, &count, &totalBytes, &error);
                }
            }
        }
        Check(rc, &error);

        List<KeyEntry> entries = new List<KeyEntry>(checked((int)count));
        int offset = 0;
        for (int i = 0; i < lengths.Length; i++) {
            int length = checked((int)lengths[i]);
            entries.Add(new KeyEntry(utf8NoBom.GetString(bytes, offset, length), kinds[i]));
            offset += length;
        }
        return entries;
    }


    /// <summary>Packed kind of the value under <paramref name="key"/>, or -1 if absent.</summary>
    internal static int KindOf(IntPtr collection, byte[] key) {
        fixed (byte* keyPtr = key) {
            return NativeBindings.fon_collection_kind_of(collection, keyPtr, key.Length);
        }
    }


    internal static bool RemoveKey(IntPtr collection, byte[] key) {
        fixed (byte* keyPtr = key) {
            return NativeBindings.fon_collection_remove(collection, keyPtr, key.Length) == 1;
        }
    }



    // ==================== COLLECTION: PUT ====================

    internal static void PutScalar<T>(IntPtr collection, byte[] key, T value, int mode, ScalarPut<T> put) where T : unmanaged {
        FonError error = default;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = put(collection, keyPtr, key.Length, value, mode, &error);
        }
        Check(rc, &error);
    }


    internal static void PutBytes(IntPtr collection, byte[] key, byte[] value, int mode, BytesPut put) {
        FonError error = default;
        int rc;
        fixed (byte* keyPtr = key) {
            fixed (byte* valuePtr = value) {
                rc = put(collection, keyPtr, key.Length, valuePtr, value.Length, mode, &error);
            }
        }
        Check(rc, &error);
    }


    internal static void PutArray<T>(IntPtr collection, byte[] key, T[] values, int mode, ArrayPut<T> put) where T : unmanaged {
        FonError error = default;
        int rc;
        fixed (byte* keyPtr = key) {
            fixed (T* valuesPtr = values) {
                rc = put(collection, keyPtr, key.Length, valuesPtr, values.Length, mode, &error);
            }
        }
        Check(rc, &error);
    }


    internal static void PutStringArray(IntPtr collection, byte[] key, string[] items, int mode) {
        byte[][] encoded = new byte[items.Length][];
        long[] lengths = new long[items.Length];
        long total = 0;
        for (int i = 0; i < items.Length; i++) {
            encoded[i] = Encode(items[i]);
            lengths[i] = encoded[i].Length;
            total += encoded[i].Length;
        }

        byte[] bytes = new byte[total];
        int offset = 0;
        foreach (byte[] item in encoded) {
            Buffer.BlockCopy(item, 0, bytes, offset, item.Length);
            offset += item.Length;
        }

        FonError error = default;
        int rc;
        fixed (byte* keyPtr = key) {
            fixed (long* lengthPtr = lengths) {
                fixed (byte* bytePtr = bytes) {
                    rc = NativeBindings.fon_collection_put_string_array(collection, keyPtr, key.Length, lengthPtr, bytePtr, items.Length, mode, &error);
                }
            }
        }
        Check(rc, &error);
    }


    internal static void PutChild(IntPtr collection, byte[] key, IntPtr child, int mode) {
        FonError error = default;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = NativeBindings.fon_collection_put_collection(collection, keyPtr, key.Length, child, mode, &error);
        }
        Check(rc, &error);
    }


    internal static void PutChildren(IntPtr collection, byte[] key, IntPtr[] children, int mode) {
        FonError error = default;
        int rc;
        fixed (byte* keyPtr = key) {
            fixed (IntPtr* childrenPtr = children) {
                rc = NativeBindings.fon_collection_put_collection_array(collection, keyPtr, key.Length, childrenPtr, children.Length, mode, &error);
            }
        }
        Check(rc, &error);
    }



    // ==================== COLLECTION: GET ====================

    internal static T GetScalar<T>(IntPtr collection, byte[] key, ScalarGet<T> get) where T : unmanaged {
        FonError error = default;
        T value = default;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = get(collection, keyPtr, key.Length, &value, &error);
        }
        Check(rc, &error);
        return value;
    }


    /// <summary>Size-query read of a string or raw value (two native calls, exact allocation).</summary>
    internal static byte[] GetBytes(IntPtr collection, byte[] key, BytesGet get) {
        FonError error = default;
        long actual = 0;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = get(collection, keyPtr, key.Length, null, 0, &actual, &error);
            Check(rc, &error);
            if (actual == 0) {
                return Array.Empty<byte>();
            }

            byte[] buffer = new byte[actual];
            fixed (byte* bufferPtr = buffer) {
                rc = get(collection, keyPtr, key.Length, bufferPtr, actual, &actual, &error);
            }
            Check(rc, &error);
            return buffer;
        }
    }


    /// <summary>Size-query read of a fixed-width array (two native calls, exact allocation).</summary>
    internal static T[] GetArray<T>(IntPtr collection, byte[] key, ArrayGet<T> get) where T : unmanaged {
        FonError error = default;
        long actual = 0;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = get(collection, keyPtr, key.Length, null, 0, &actual, &error);
            Check(rc, &error);
            if (actual == 0) {
                return Array.Empty<T>();
            }

            T[] buffer = new T[actual];
            fixed (T* bufferPtr = buffer) {
                rc = get(collection, keyPtr, key.Length, bufferPtr, actual, &actual, &error);
            }
            Check(rc, &error);
            return buffer;
        }
    }


    /// <summary>Multi-string read of a string array in one size query and one fill call.</summary>
    internal static string[] GetStringArray(IntPtr collection, byte[] key) {
        FonError error = default;
        long count = 0;
        long totalBytes = 0;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = NativeBindings.fon_collection_get_string_array(collection, keyPtr, key.Length, null, 0, null, 0, &count, &totalBytes, &error);
            Check(rc, &error);
            if (count == 0) {
                return Array.Empty<string>();
            }

            long[] lengths = new long[count];
            byte[] bytes = new byte[totalBytes];
            fixed (long* lengthPtr = lengths) {
                fixed (byte* bytePtr = bytes) {
                    rc = NativeBindings.fon_collection_get_string_array(collection, keyPtr, key.Length, lengthPtr, count, bytePtr, totalBytes, &count, &totalBytes, &error);
                }
            }
            Check(rc, &error);

            string[] items = new string[lengths.Length];
            int offset = 0;
            for (int i = 0; i < lengths.Length; i++) {
                int length = checked((int)lengths[i]);
                items[i] = utf8NoBom.GetString(bytes, offset, length);
                offset += length;
            }
            return items;
        }
    }


    /// <summary>Borrowed handle of a nested collection; fails when the key is absent or not a collection.</summary>
    internal static IntPtr GetChild(IntPtr collection, byte[] key) {
        FonError error = default;
        IntPtr child;
        fixed (byte* keyPtr = key) {
            child = NativeBindings.fon_collection_get_collection(collection, keyPtr, key.Length, &error);
        }
        if (child == IntPtr.Zero) {
            throw CreateException(error.Code, &error);
        }
        return child;
    }


    /// <summary>Borrowed handles of an array of nested collections (two-call pattern).</summary>
    internal static IntPtr[] GetChildren(IntPtr collection, byte[] key) {
        FonError error = default;
        long actual = 0;
        int rc;
        fixed (byte* keyPtr = key) {
            rc = NativeBindings.fon_collection_get_collection_array(collection, keyPtr, key.Length, null, 0, &actual, &error);
            Check(rc, &error);
            if (actual == 0) {
                return Array.Empty<IntPtr>();
            }

            IntPtr[] buffer = new IntPtr[actual];
            fixed (IntPtr* bufferPtr = buffer) {
                rc = NativeBindings.fon_collection_get_collection_array(collection, keyPtr, key.Length, bufferPtr, actual, &actual, &error);
            }
            Check(rc, &error);
            return buffer;
        }
    }



    // ==================== COLLECTION: CLONE ====================

    internal static IntPtr CloneCollection(IntPtr collection) {
        FonError error = default;
        IntPtr clone = NativeBindings.fon_collection_clone(collection, &error);
        if (clone == IntPtr.Zero) {
            throw CreateException(error.Code, &error);
        }
        return clone;
    }



    // ==================== DUMP ====================

    internal static ulong[] ReadDumpIds(IntPtr dump) {
        FonError error = default;
        long count = 0;
        int rc = NativeBindings.fon_dump_ids(dump, null, 0, &count, &error);
        Check(rc, &error);
        if (count == 0) {
            return Array.Empty<ulong>();
        }

        ulong[] ids = new ulong[count];
        fixed (ulong* idPtr = ids) {
            rc = NativeBindings.fon_dump_ids(dump, idPtr, count, &count, &error);
        }
        Check(rc, &error);
        return ids;
    }


    /// <summary>Borrowed record handle, or <see cref="IntPtr.Zero"/> if the id is absent.</summary>
    internal static IntPtr DumpGet(IntPtr dump, ulong id) {
        return NativeBindings.fon_dump_get(dump, id);
    }


    internal static void DumpPut(IntPtr dump, ulong id, IntPtr collection, int mode) {
        FonError error = default;
        int rc = NativeBindings.fon_dump_put(dump, id, collection, mode, &error);
        Check(rc, &error);
    }


    internal static bool DumpRemove(IntPtr dump, ulong id) {
        return NativeBindings.fon_dump_remove(dump, id) == 1;
    }


    internal static long DumpRemoveMany(IntPtr dump, ulong[] ids) {
        fixed (ulong* idPtr = ids) {
            return NativeBindings.fon_dump_remove_many(dump, idPtr, ids.Length);
        }
    }


    internal static bool DumpGens(IntPtr dump, ulong id, out uint recordGen, out uint treeGen) {
        uint record = 0;
        uint tree = 0;
        int found = NativeBindings.fon_dump_gens(dump, id, &record, &tree);
        recordGen = record;
        treeGen = tree;
        return found == 1;
    }


    internal static bool DumpTouch(IntPtr dump, ulong id) {
        return NativeBindings.fon_dump_touch(dump, id) == 1;
    }



    // ==================== SERIALIZE / DESERIALIZE ====================

    internal static string SerializeCollection(IntPtr collection) {
        FonError error = default;
        byte* buffer = null;
        long length = 0;
        int rc = NativeBindings.fon_serialize_collection(collection, &buffer, &length, &error);
        Check(rc, &error);
        try {
            return DecodeUtf8(buffer, length);
        } finally {
            NativeBindings.fon_buffer_free(buffer, length);
        }
    }


    internal static string SerializeDump(IntPtr dump, int maxThreads) {
        FonError error = default;
        byte* buffer = null;
        long length = 0;
        int rc = NativeBindings.fon_serialize_dump(dump, maxThreads, &buffer, &length, &error);
        Check(rc, &error);
        try {
            return DecodeUtf8(buffer, length);
        } finally {
            NativeBindings.fon_buffer_free(buffer, length);
        }
    }


    internal static void SerializeDumpToFile(IntPtr dump, string path, int maxThreads) {
        FonError error = default;
        byte[] pathBytes = Encode(path);
        int rc;
        fixed (byte* pathPtr = pathBytes) {
            rc = NativeBindings.fon_serialize_to_file(dump, pathPtr, pathBytes.Length, maxThreads, &error);
        }
        Check(rc, &error);
    }


    internal static IntPtr DeserializeDump(ReadOnlySpan<byte> utf8, int maxThreads) {
        FonError error = default;
        IntPtr handle;
        fixed (byte* dataPtr = utf8) {
            handle = NativeBindings.fon_deserialize_dump(dataPtr, utf8.Length, maxThreads, &error);
        }
        if (handle == IntPtr.Zero) {
            throw CreateException(error.Code, &error);
        }
        return handle;
    }


    internal static IntPtr DeserializeCollection(ReadOnlySpan<byte> utf8) {
        FonError error = default;
        IntPtr handle;
        fixed (byte* dataPtr = utf8) {
            handle = NativeBindings.fon_deserialize_collection(dataPtr, utf8.Length, &error);
        }
        if (handle == IntPtr.Zero) {
            throw CreateException(error.Code, &error);
        }
        return handle;
    }


    internal static IntPtr DeserializeFromFile(string path, int maxThreads) {
        FonError error = default;
        byte[] pathBytes = Encode(path);
        IntPtr handle;
        fixed (byte* pathPtr = pathBytes) {
            handle = NativeBindings.fon_deserialize_from_file(pathPtr, pathBytes.Length, maxThreads, &error);
        }
        if (handle == IntPtr.Zero) {
            throw CreateException(error.Code, &error);
        }
        return handle;
    }
}
