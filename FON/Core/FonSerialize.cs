using FON.Native;
using FON.Types;

namespace FON.Core;


public partial class Fon {
    // ==================== IN-MEMORY ====================

    /// <summary>Serializes a single record to a UTF-8 string (one .fon line, no trailing newline).</summary>
    public static string SerializeToString(FonCollection collection) {
        ArgumentNullException.ThrowIfNull(collection);
        return collection.Read(raw => NativeApi.SerializeCollection(raw));
    }


    /// <summary>Serializes an entire dump to a UTF-8 string (one .fon line per record, newline-terminated).</summary>
    public static string SerializeToString(FonDump dump) {
        ArgumentNullException.ThrowIfNull(dump);
        return dump.Read(raw => NativeApi.SerializeDump(raw, 0));
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
        int maxThreads = maxDegreeOfParallelism ?? 0;
        string path = fileInfo.FullName;
        dump.Use(raw => NativeApi.SerializeDumpToFile(raw, path, maxThreads));
    }
}
