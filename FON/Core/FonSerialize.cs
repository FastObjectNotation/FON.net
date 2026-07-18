using FON.Native;
using FON.Types;

namespace FON.Core;


public partial class Fon {
    // ==================== IN-MEMORY ====================

    /// <summary>Serializes a single record to a UTF-8 string (one .fon line, no trailing newline).</summary>
    public static string SerializeToString(FonCollection collection) {
        ArgumentNullException.ThrowIfNull(collection);
        collection.ThrowIfDisposed();
        IntPtr raw = collection.Handle.DangerousGetHandle();
        try {
            return NativeApi.SerializeCollection(raw);
        } finally {
            GC.KeepAlive(collection.Handle);
        }
    }


    /// <summary>Serializes an entire dump to a UTF-8 string (one .fon line per record, newline-terminated).</summary>
    public static string SerializeToString(FonDump dump) {
        ArgumentNullException.ThrowIfNull(dump);
        IntPtr raw = dump.Handle.DangerousGetHandle();
        try {
            return NativeApi.SerializeDump(raw);
        } finally {
            GC.KeepAlive(dump.Handle);
        }
    }


    // ==================== FILE ====================

    /// <remarks>
    /// All SerializeToFile* overloads route through the same native file writer;
    /// the distinct names/parameters are retained for source compatibility with
    /// earlier versions that chose between managed serialization strategies.
    /// </remarks>
    public static Task SerializeToFileAsync(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => SerializeToFileCore(dump, fileInfo, maxDegreeOfParallelism));


    public static Task SerializeToFilePipelineAsync(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => SerializeToFileCore(dump, fileInfo, maxDegreeOfParallelism));


    public static Task SerializeToFileChunkedAsync(FonDump dump, FileInfo fileInfo, int chunkSize = 1000, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => SerializeToFileCore(dump, fileInfo, maxDegreeOfParallelism));


    public static Task SerializeToFileAutoAsync(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => SerializeToFileCore(dump, fileInfo, maxDegreeOfParallelism));


    /// <summary>Synchronous version for compatibility.</summary>
    public static void SerializeToFileAuto(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) =>
        SerializeToFileCore(dump, fileInfo, maxDegreeOfParallelism);


    private static void SerializeToFileCore(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism) {
        ArgumentNullException.ThrowIfNull(dump);
        ArgumentNullException.ThrowIfNull(fileInfo);
        int maxThreads = maxDegreeOfParallelism ?? Environment.ProcessorCount;
        var error = new FonError();
        IntPtr raw = dump.Handle.DangerousGetHandle();
        int rc;
        try {
            rc = NativeBindings.fon_serialize_to_file(raw, fileInfo.FullName, maxThreads, ref error);
        } finally {
            GC.KeepAlive(dump.Handle);
        }
        if (rc != FonResultCode.OK) {
            throw new FonNativeException(error);
        }
    }
}
