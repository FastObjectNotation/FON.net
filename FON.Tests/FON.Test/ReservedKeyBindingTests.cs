using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Keys containing syntax characters or line breaks are rejected before any write, and a
/// collection passed as the value of a rejected put stays owned and usable.
/// </summary>
public class ReservedKeyBindingTests {
    [Theory]
    [InlineData("a=b")]
    [InlineData("quote\"key")]
    [InlineData("open{key")]
    [InlineData("close}key")]
    [InlineData("open[key")]
    [InlineData("close]key")]
    [InlineData("carriage\rreturn")]
    [InlineData("line\nfeed")]
    public void ReservedKeyCharacters_AddIsArgumentError(string key) {
        using var collection = new FonCollection();

        Assert.Throws<ArgumentException>(() => collection.Add(key, 1));

        Assert.False(collection.ContainsKey(key));
        Assert.Equal(0, collection.Count);
    }


    [Theory]
    [InlineData("a=b")]
    [InlineData("line\nfeed")]
    public void ReservedKeyCharacters_SetIsArgumentError(string key) {
        using var collection = new FonCollection();

        Assert.Throws<ArgumentException>(() => collection.Set(key, 1));

        Assert.Equal(0, collection.Count);
    }


    [Fact]
    public void RejectedPut_LeavesPassedCollectionOwnedAndUsable() {
        using var parent = new FonCollection();
        var child = new FonCollection { { "y", 2 } };

        Assert.Throws<ArgumentException>(() => parent.Add("bad=key", child));

        Assert.Equal(2, child.Get<int>("y"));
        parent.Add("good", child);
        Assert.Equal(2, parent.Get<FonCollection>("good").Get<int>("y"));
    }


    [Fact]
    public void RejectedDumpPut_OnExistingId_LeavesRecordOwnedAndUsable() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "x", 1 } });
        var child = new FonCollection { { "y", 2 } };

        Assert.Throws<InvalidOperationException>(() => dump.Add(0, child));

        Assert.Equal(2, child.Get<int>("y"));
        dump.Add(1, child);
        Assert.Equal(2, dump[1].Get<int>("y"));
        Assert.Equal(1, dump[0].Get<int>("x"));
    }
}
