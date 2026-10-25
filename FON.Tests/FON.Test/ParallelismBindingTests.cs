using FON.Core;
using FON.Native;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Thread limits must not change the output, and a missing file reports file-not-found.
/// </summary>
public class ParallelismBindingTests : IDisposable {
    private readonly string testDir = Path.Combine(Path.GetTempPath(), $"fon_parallel_{Guid.NewGuid():N}");


    public ParallelismBindingTests() {
        Directory.CreateDirectory(testDir);
    }


    public void Dispose() {
        if (Directory.Exists(testDir)) {
            Directory.Delete(testDir, recursive: true);
        }
    }


    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void MaxThreads_SerializeToFile_ProducesSameBytesAsDefault(int threads) {
        using var dump = BuildDump(2000);
        var defaultFile = new FileInfo(Path.Combine(testDir, "default.fon"));
        var limitedFile = new FileInfo(Path.Combine(testDir, $"limited-{threads}.fon"));

        Fon.SerializeToFileAuto(dump, defaultFile, 0);
        Fon.SerializeToFileAuto(dump, limitedFile, threads);

        Assert.Equal(File.ReadAllBytes(defaultFile.FullName), File.ReadAllBytes(limitedFile.FullName));
    }


    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task MaxThreads_DeserializeFromFile_ProducesSameRecordsAsDefault(int threads) {
        using var dump = BuildDump(2000);
        var file = new FileInfo(Path.Combine(testDir, "records.fon"));
        await Fon.SerializeToFileAutoAsync(dump, file);

        using var fromDefault = await Fon.DeserializeFromFileAutoAsync(file);
        using var fromLimited = await Fon.DeserializeFromFileAsync(file, threads);

        Assert.Equal(Fon.SerializeToString(fromDefault), Fon.SerializeToString(fromLimited));
        Assert.Equal(2000, fromLimited.Count);
    }


    [Fact]
    public async Task DeserializeMissingFile_ThrowsFileNotFound() {
        var missing = new FileInfo(Path.Combine(testDir, "absent.fon"));

        var exception = await Assert.ThrowsAsync<FonNativeException>(() => Fon.DeserializeFromFileAutoAsync(missing));

        Assert.Equal(FonResultCode.FileNotFound, exception.Code);
    }


    private static FonDump BuildDump(int records) {
        var dump = new FonDump();
        for (int i = 0; i < records; i++) {
            dump.Add((ulong)i, new FonCollection {
                { "id", i },
                { "name", $"record-{i}" },
                { "tags", new List<string> { "a", i.ToString() } }
            });
        }
        return dump;
    }
}
