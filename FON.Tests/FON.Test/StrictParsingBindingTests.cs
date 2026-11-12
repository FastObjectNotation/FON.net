using FON.Core;
using FON.Native;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Malformed text is an error, an empty line is an empty record, and text that cannot be
/// written as UTF-8 is rejected instead of being altered.
/// </summary>
public class StrictParsingBindingTests {
    [Theory]
    [InlineData("a=b:[[]]")]
    [InlineData("a=i:12abc")]
    [InlineData("a=i:+")]
    [InlineData("a=b:false")]
    [InlineData("a=i:1,junk")]
    [InlineData("a=i:1,a=i:2")]
    [InlineData("a=i:1,")]
    [InlineData("a=i:[1,,2]")]
    [InlineData("a=s:\"\\q\"")]
    public void MalformedRecord_Throws(string text) {
        FonNativeException failure = Assert.Throws<FonNativeException>(() => Fon.DeserializeCollection(text));

        Assert.Equal(FonResultCode.ParseFailed, failure.Code);
    }


    [Fact]
    public void DumpParseError_NamesTheLine() {
        FonNativeException failure = Assert.Throws<FonNativeException>(() => Fon.Deserialize("a=i:1\na=b:[[]]\n"));

        Assert.StartsWith("line 2: ", failure.Message);
    }


    [Fact]
    public void EmptyRecord_SurvivesSaveAndLoad() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "x", 1 } });
        dump.Add(1, new FonCollection());
        dump.Add(2, new FonCollection { { "x", 3 } });

        string text = Fon.SerializeToString(dump);
        using FonDump loaded = Fon.Deserialize(text);

        Assert.Equal("x=i:1\n\nx=i:3\n", text);
        Assert.Equal(3, loaded.Count);
        Assert.Equal(0, loaded[1].Count);
        Assert.Equal(3, loaded[2].Get<int>("x"));
    }


    [Fact]
    public void LoneSurrogate_InAValueOrAKey_Throws() {
        using var collection = new FonCollection();

        Assert.ThrowsAny<ArgumentException>(() => collection.Add("s", "a\uD800b"));
        Assert.ThrowsAny<ArgumentException>(() => collection.Add("k\uDC00", 1));
        Assert.ThrowsAny<ArgumentException>(() => collection.Add("list", new List<string> { "ok", "\uD83D" }));
        Assert.Equal(0, collection.Count);
    }


    [Fact]
    public void Enumeration_SkipsARecordRemovedMeanwhile() {
        using var dump = new FonDump();
        for (ulong id = 0; id < 5; id++) {
            dump.Add(id, new FonCollection { { "n", (int)id } });
        }

        var seen = new List<ulong>();
        foreach (KeyValuePair<ulong, FonCollection> pair in dump) {
            seen.Add(pair.Key);
            if (pair.Key == 1) {
                dump.Remove(3);
            }
        }

        Assert.Equal(new ulong[] { 0, 1, 2, 4 }, seen);
    }


    [Fact]
    public void Root_CannotBeStoredInsideItsOwnNestedCollection() {
        using var root = new FonCollection { { "child", new FonCollection { { "leaf", new FonCollection() } } } };
        FonCollection leaf = root.Get<FonCollection>("child").Get<FonCollection>("leaf");

        Assert.Throws<ArgumentException>(() => leaf.Add("back", root));
        Assert.Equal("child=o:{leaf=o:{}}", Fon.SerializeToString(root));
    }
}
