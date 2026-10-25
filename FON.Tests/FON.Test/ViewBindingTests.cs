using FON.Core;
using FON.Native;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Views obtained from a dump or from a nested getter: validity after their own edits, staleness
/// after the data they refer to changes, disposal of owners, and owners kept alive by views.
/// </summary>
public class ViewBindingTests {
    [Fact]
    public void RecordView_SurvivesRepeatedSetOnItself() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "w", 1 }, { "name", "a" } });
        var record = dump[0];

        for (int i = 0; i < 5; i++) {
            record.Set("w", record.Get<int>("w") * 2);
        }
        record.Set("name", "b");

        Assert.Equal(32, record.Get<int>("w"));
        Assert.Equal("b", record.Get<string>("name"));
        Assert.Equal(32, dump[0].Get<int>("w"));
    }


    [Fact]
    public void RecordView_GoesStaleAfterItsRecordIsRemoved() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "w", 1 } });
        var record = dump[0];

        dump.Remove(0);

        Assert.Throws<FonStaleViewException>(() => record.Get<int>("w"));
    }


    [Fact]
    public void RecordView_GoesStaleAfterItsRecordIsReplaced() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "w", 1 } });
        var record = dump[0];

        dump.Set(0, new FonCollection { { "w", 2 } });

        Assert.Throws<FonStaleViewException>(() => record.Get<int>("w"));
        Assert.Equal(2, dump[0].Get<int>("w"));
    }


    [Fact]
    public void RecordView_OfAnotherId_IsUnaffected() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "w", 10 } });
        dump.Add(1, new FonCollection { { "w", 11 } });
        var other = dump[1];

        dump.Remove(0);
        dump.Set(2, new FonCollection { { "w", 12 } });

        Assert.Equal(11, other.Get<int>("w"));
    }


    [Fact]
    public void NestedView_DanglingChild_ThrowsStaleAfterFieldOverwrittenWithScalar() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection { { "inner", new FonCollection { { "x", 1 } } } });
        var record = dump[0];
        var nested = record.Get<FonCollection>("inner");

        record.Set("inner", 5);

        Assert.Throws<FonStaleViewException>(() => nested.Get<int>("x"));
    }


    [Fact]
    public void NestedView_GoesStaleAfterAnyFieldChangeInItsRecord() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection {
            { "inner", new FonCollection { { "x", 1 } } },
            { "other", 1 }
        });
        var nested = dump[0].Get<FonCollection>("inner");

        dump[0].Set("other", 2);

        Assert.Throws<FonStaleViewException>(() => nested.Get<int>("x"));
    }


    [Fact]
    public void NestedView_MutationKeepsItValid_AndMakesSiblingsStale() {
        using var dump = new FonDump();
        dump.Add(0, new FonCollection {
            { "first", new FonCollection { { "x", 1 } } },
            { "second", new FonCollection { { "y", 1 } } }
        });
        var record = dump[0];
        var first = record.Get<FonCollection>("first");
        var second = record.Get<FonCollection>("second");

        first.Set("x", 2);

        Assert.Equal(2, first.Get<int>("x"));
        Assert.Throws<FonStaleViewException>(() => second.Get<int>("y"));
    }


    [Fact]
    public void NestedView_OfOwnedCollection_GoesStaleAfterOwnerIsMutated() {
        using var owner = new FonCollection { { "inner", new FonCollection { { "x", 1 } } } };
        var nested = owner.Get<FonCollection>("inner");

        owner.Set("extra", 1);

        Assert.Throws<FonStaleViewException>(() => nested.Get<int>("x"));
    }


    [Fact]
    public void NestedView_OfOwnedCollection_StaysValidAfterItsOwnEdit() {
        using var owner = new FonCollection { { "inner", new FonCollection { { "x", 1 } } } };
        var nested = owner.Get<FonCollection>("inner");

        nested.Set("x", 7);

        Assert.Equal(7, nested.Get<int>("x"));
        Assert.Equal(7, owner.Get<FonCollection>("inner").Get<int>("x"));
    }


    [Fact]
    public void RecordView_AfterDumpDisposed_ThrowsObjectDisposed() {
        var dump = new FonDump();
        dump.Add(0, new FonCollection { { "w", 1 } });
        var record = dump[0];

        dump.Dispose();

        Assert.Throws<ObjectDisposedException>(() => record.Get<int>("w"));
    }


    [Fact]
    public void NestedView_AfterOwnerDisposed_ThrowsObjectDisposed() {
        var owner = new FonCollection { { "inner", new FonCollection { { "x", 1 } } } };
        var nested = owner.Get<FonCollection>("inner");

        owner.Dispose();

        Assert.Throws<ObjectDisposedException>(() => nested.Get<int>("x"));
    }


    [Fact]
    public void RecordView_KeepsItsDumpAlive_AfterAllOtherReferencesAreDropped() {
        var record = MakeRecordViewOfTemporaryDump();

        ForceCollection();

        Assert.Equal(5, record.Get<int>("v"));
    }


    [Fact]
    public void NestedView_KeepsItsRootAlive_AfterAllOtherReferencesAreDropped() {
        var nested = MakeNestedViewOfTemporaryOwner();

        ForceCollection();

        Assert.Equal(6, nested.Get<int>("x"));
    }


    [Fact]
    public void RecordView_OfDeserializedDump_SurvivesRemoveOfAbsentId() {
        using var dump = Fon.Deserialize(MakeTwoRecordText());
        var record = dump[0];

        Assert.False(dump.Remove(999));

        Assert.Equal(1, record.Get<int>("a"));
    }


    [Fact]
    public void RecordView_OfDeserializedDump_SurvivesRemoveManyOfAbsentIds() {
        using var dump = Fon.Deserialize(MakeTwoRecordText());
        var record = dump[0];

        Assert.Equal(0, dump.RemoveMany(new ulong[] { 999 }));

        Assert.Equal(1, record.Get<int>("a"));
    }


    [Fact]
    public void RecordView_OfDeserializedDump_SurvivesRemoveManyOfNoIds() {
        using var dump = Fon.Deserialize(MakeTwoRecordText());
        var record = dump[0];

        Assert.Equal(0, dump.RemoveMany(Array.Empty<ulong>()));

        Assert.Equal(1, record.Get<int>("a"));
    }


    private static string MakeTwoRecordText() {
        using var source = new FonDump();
        source.Add(0, new FonCollection { { "a", 1 } });
        source.Add(1, new FonCollection { { "a", 2 } });
        return Fon.SerializeToString(source);
    }


    private static FonCollection MakeRecordViewOfTemporaryDump() {
        var dump = new FonDump();
        dump.Add(0, new FonCollection { { "v", 5 } });
        return dump[0];
    }


    private static FonCollection MakeNestedViewOfTemporaryOwner() {
        var owner = new FonCollection { { "inner", new FonCollection { { "x", 6 } } } };
        return owner.Get<FonCollection>("inner");
    }


    private static void ForceCollection() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
