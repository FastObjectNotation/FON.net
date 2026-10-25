using FON.Native;
using FON.Types;

namespace FON.Core;


public partial class Fon {
    // ==================== IN-MEMORY ====================

    /// <summary>Parses a multi-line UTF-8 string into a new <see cref="FonDump"/>.</summary>
    public static FonDump Deserialize(string text) {
        ArgumentNullException.ThrowIfNull(text);
        IntPtr raw = NativeApi.DeserializeDump(NativeApi.Encode(text), 0);
        return FonDump.FromOwnedHandle(raw, EstimateBytes(text.Length));
    }


    /// <summary>Parses a multi-line UTF-8 buffer into a new <see cref="FonDump"/>.</summary>
    public static FonDump Deserialize(ReadOnlySpan<byte> utf8) {
        IntPtr raw = NativeApi.DeserializeDump(utf8, 0);
        return FonDump.FromOwnedHandle(raw, EstimateBytes(utf8.Length));
    }


    /// <summary>Parses a single-line UTF-8 string into a new <see cref="FonCollection"/>.</summary>
    public static FonCollection DeserializeCollection(string text) {
        ArgumentNullException.ThrowIfNull(text);
        IntPtr raw = NativeApi.DeserializeCollection(NativeApi.Encode(text));
        return FonCollection.FromOwnedHandle(raw, EstimateBytes(text.Length));
    }


    /// <summary>Parses a single-line UTF-8 buffer into a new <see cref="FonCollection"/>.</summary>
    public static FonCollection DeserializeCollection(ReadOnlySpan<byte> utf8) {
        IntPtr raw = NativeApi.DeserializeCollection(utf8);
        return FonCollection.FromOwnedHandle(raw, EstimateBytes(utf8.Length));
    }


    // ==================== FILE ====================

    /// <remarks>
    /// All DeserializeFromFile* overloads route through the same native file parser;
    /// the distinct names/parameters are retained for source compatibility with
    /// earlier versions that chose between managed parsing strategies.
    /// </remarks>
    public static Task<FonDump> DeserializeFromFileAsync(FileInfo file, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => DeserializeFromFileCore(file, maxDegreeOfParallelism));


    public static Task<FonDump> DeserializeFromFileChunkedAsync(FileInfo file, int chunkSize = 10000, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => DeserializeFromFileCore(file, maxDegreeOfParallelism));


    public static Task<FonDump> DeserializeFromFileAutoAsync(FileInfo file, int? maxDegreeOfParallelism = null) =>
        Task.Run(() => DeserializeFromFileCore(file, maxDegreeOfParallelism));


    /// <summary>
    /// Reads a file into a new dump. A missing file throws <see cref="FonNativeException"/> with
    /// code <see cref="FonResultCode.FileNotFound"/>.
    /// </summary>
    private static FonDump DeserializeFromFileCore(FileInfo file, int? maxDegreeOfParallelism) {
        ArgumentNullException.ThrowIfNull(file);
        int maxThreads = maxDegreeOfParallelism ?? 0;
        IntPtr raw = NativeApi.DeserializeFromFile(file.FullName, maxThreads);
        return FonDump.FromOwnedHandle(raw, EstimateBytes(file.Exists ? file.Length : 0));
    }
}
