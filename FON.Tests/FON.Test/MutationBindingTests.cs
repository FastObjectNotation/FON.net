using FON.Core;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Mutation verbs: add (must not exist), set (upsert), replace (must exist), remove and
/// removeMany, plus their effect on other ids and on serialized output.
/// </summary>
public class MutationBindingTests {
    [Fact]
    public void CollectionAdd_ExistingKey_ThrowsAlreadyExists() {
        using var collection = new FonCollection { { "key", 1 } };

        Assert.Throws<InvalidOperationException>(() => collection.Add("key", 2));
        Assert.Equal(1, collection.Get<int>("key"));
    }


    [Fact]
    public void CollectionSet_IsUpsert_AndMayChangeType() {
        using var collection = new FonCollection();

        collection.Set("key", 1);
        collection.Set("key", "now text");
        collection["other"] = 5;
        collection["other"] = 6;

        Assert.Equal("now text", collection.Get<string>("key"));
        Assert.Equal(6, collection.Get<int>("other"));
        Assert.Equal(2, collection.Count);
    }


    [Fact]
    public void CollectionRemove_ReturnsTrueThenFalse_AndDropsTheKey() {
        using var collection = new FonCollection { { "a", 1 }, { "b", 2 } };

        Assert.True(collection.Remove("a"));
        Assert.False(collection.Remove("a"));

        Assert.False(collection.ContainsKey("a"));
        Assert.Equal(new[] { "b" }, collection.Keys);
        Assert.Equal(2, collection.Get<int>("b"));
    }


    [Fact]
    public void CollectionClone_IsDeepAndIndependent() {
        using var original = new FonCollection { { "inner", new FonCollection { { "x", 1 } } } };
        using var copy = original.Clone();

        copy.Get<FonCollection>("inner").Set("x", 99);
        copy.Set("extra", true);

        Assert.Equal(1, original.Get<FonCollection>("inner").Get<int>("x"));
        Assert.False(original.ContainsKey("extra"));
        Assert.Equal(99, copy.Get<FonCollection>("inner").Get<int>("x"));
    }


    [Fact]
    public void DumpAdd_ExistingId_ThrowsAlreadyExists() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "first", 1 } });

        Assert.Throws<InvalidOperationException>(() => dump.Add(0, new FonCollection { { "second", 2 } }));
        Assert.Equal(1, dump[0].Get<int>("first"));
    }


    [Fact]
    public void DumpSet_IsUpsert() {
        using var dump = new FonDump();

        dump.Set(3, new FonCollection { { "v", 1 } });
        dump.Set(3, new FonCollection { { "v", 2 } });
        dump[3] = new FonCollection { { "v", 3 } };

        Assert.Equal(1, dump.Count);
        Assert.Equal(3, dump[3].Get<int>("v"));
    }


    [Fact]
    public void DumpReplace_AbsentId_ThrowsKeyNotFound() {
        using var dump = new FonDump();

        Assert.Throws<KeyNotFoundException>(() => dump.Replace(7, new FonCollection { { "v", 1 } }));
        Assert.Equal(0, dump.Count);
    }


    [Fact]
    public void DumpReplace_ExistingId_SwapsTheRecord() {
        using var dump = new FonDump();
        dump.Add(7, new FonCollection { { "v", 1 } });

        dump.Replace(7, new FonCollection { { "v", 2 } });

        Assert.Equal(2, dump.Get(7).Get<int>("v"));
    }


    [Fact]
    public void DumpRemove_ReturnsTrueThenFalse() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "x", 1 } });

        Assert.True(dump.Remove(0));
        Assert.False(dump.Remove(0));
        Assert.False(dump.ContainsId(0));
    }


    [Fact]
    public void DumpRemoveMany_CountsOnlyLiveDistinctIds() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "x", 0 } });
        dump.Add(1, new FonCollection { { "x", 1 } });
        dump.Add(2, new FonCollection { { "x", 2 } });

        int removed = dump.RemoveMany(new ulong[] { 0, 0, 5, 2, 2, 42 });

        Assert.Equal(2, removed);
        Assert.Equal(new ulong[] { 1 }, dump.Ids);
    }


    [Fact]
    public void DumpRemove_LeavesOtherIdsUnchanged() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "x", 0 } });
        dump.Add(1, new FonCollection { { "x", 1 } });
        dump.Add(2, new FonCollection { { "x", 2 } });

        dump.Remove(1);

        Assert.Equal(0, dump[0].Get<int>("x"));
        Assert.Equal(2, dump[2].Get<int>("x"));
    }


    [Fact]
    public void SerializeAfterRemove_RenumbersLines() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "x", 0 } });
        dump.Add(1, new FonCollection { { "x", 1 } });
        dump.Add(2, new FonCollection { { "x", 2 } });
        dump.Remove(1);

        string text = Fon.SerializeToString(dump);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("x=i:0", lines[0]);
        Assert.Contains("x=i:2", lines[1]);
        using var reloaded = Fon.Deserialize(text);
        Assert.Equal(new ulong[] { 0, 1 }, reloaded.Ids);
    }
}
