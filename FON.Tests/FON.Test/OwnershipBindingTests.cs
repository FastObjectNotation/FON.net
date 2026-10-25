using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Ownership transfer when collections are nested: views cannot be stored, a successful put
/// transfers the passed wrapper, a failed put leaves it owned, cycles and repeats are rejected.
/// </summary>
public class OwnershipBindingTests {
    [Fact]
    public void PutView_IntoAnotherContainer_ThrowsArgumentError_AndCloneThenWorks() {
        using var holder = new FonCollection { { "child", new FonCollection { { "x", 1 } } } };
        var view = holder.Get<FonCollection>("child");
        using var target = new FonCollection();

        Assert.Throws<ArgumentException>(() => target.Add("view", view));
        Assert.False(target.ContainsKey("view"));

        target.Add("copy", view.Clone());

        Assert.Equal(1, target.Get<FonCollection>("copy").Get<int>("x"));
        Assert.Equal(1, view.Get<int>("x"));
    }


    [Fact]
    public void PutView_IntoDump_ThrowsArgumentError() {
        using var holder = new FonCollection { { "child", new FonCollection { { "x", 1 } } } };
        var view = holder.Get<FonCollection>("child");
        using var dump = new FonDump();

        Assert.Throws<ArgumentException>(() => dump.Add(0, view));
        Assert.Equal(0, dump.Count);

        dump.Add(0, view.Clone());
        Assert.Equal(1, dump[0].Get<int>("x"));
    }


    [Fact]
    public void SuccessfulPut_MakesThePassedWrapperUnusable() {
        using var parent = new FonCollection();
        var child = new FonCollection { { "x", 1 } };

        parent.Add("child", child);

        Assert.Throws<ObjectDisposedException>(() => child.Get<int>("x"));
        Assert.Equal(1, parent.Get<FonCollection>("child").Get<int>("x"));
    }


    [Fact]
    public void SuccessfulDumpPut_MakesThePassedWrapperUnusable() {
        using var dump = new FonDump();
        var record = new FonCollection { { "x", 1 } };

        dump.Add(0, record);

        Assert.Throws<ObjectDisposedException>(() => record.Get<int>("x"));
        Assert.Equal(1, dump[0].Get<int>("x"));
    }


    [Fact]
    public void FailedAdd_LeavesTheChildUsableAndOwned() {
        using var parent = new FonCollection { { "taken", 1 } };
        var child = new FonCollection { { "y", 2 } };

        Assert.Throws<InvalidOperationException>(() => parent.Add("taken", child));

        Assert.Equal(2, child.Get<int>("y"));
        parent.Add("free", child);
        Assert.Equal(2, parent.Get<FonCollection>("free").Get<int>("y"));
    }


    [Fact]
    public void InsertingRootIntoItsOwnNestedView_IsAnError() {
        var root = new FonCollection { { "inner", new FonCollection { { "v", 1 } } } };
        var nested = root.Get<FonCollection>("inner");

        Assert.Throws<ArgumentException>(() => nested.Add("back", root));

        Assert.Equal(1, root.Get<FonCollection>("inner").Get<int>("v"));
        root.Dispose();
    }


    [Fact]
    public void InsertingCollectionIntoItself_IsAnError() {
        using var collection = new FonCollection();

        Assert.Throws<ArgumentException>(() => collection.Add("self", collection));
        Assert.Equal(0, collection.Count);
    }


    [Fact]
    public void SameChildTwiceInOneArray_IsAnError() {
        var child = new FonCollection { { "x", 1 } };

        Assert.Throws<ArgumentException>(() => new FonCollection {
            { "arr", new List<FonCollection> { child, child } }
        });

        Assert.Equal(1, child.Get<int>("x"));
        child.Dispose();
    }
}
