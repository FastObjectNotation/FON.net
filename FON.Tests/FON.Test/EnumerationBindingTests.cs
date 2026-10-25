using System.Diagnostics;
using System.Text;
using FON.Core;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Key and id enumeration: order, kinds, ascending ids after sparse adds and removals, and
/// linear-time enumeration of large dumps.
/// </summary>
public class EnumerationBindingTests {
    [Fact]
    public void Keys_AreInInsertionOrder_WithKinds() {
        using var collection = new FonCollection {
            { "zeta", "z" },
            { "alpha", 1 },
            { "mid", new List<double> { 0.5 } }
        };

        Assert.Equal(new[] { "zeta", "alpha", "mid" }, collection.Keys);
        Assert.Equal(new FonKind(FonType.String, false), collection.KindOf("zeta"));
        Assert.Equal(new FonKind(FonType.Int, false), collection.KindOf("alpha"));
        Assert.Equal(new FonKind(FonType.Double, true), collection.KindOf("mid"));
    }


    [Fact]
    public void Enumeration_OfCollection_YieldsEveryPairWithItsValue() {
        using var collection = new FonCollection {
            { "a", 1 },
            { "b", "two" }
        };

        var pairs = collection.ToList();

        Assert.Equal(2, pairs.Count);
        Assert.Equal("a", pairs[0].Key);
        Assert.Equal(1, pairs[0].Value);
        Assert.Equal("b", pairs[1].Key);
        Assert.Equal("two", pairs[1].Value);
    }


    [Fact]
    public void DumpIds_AreAscendingAfterSparseAddsAndRemovals() {
        using var dump = new FonDump();
        foreach (ulong id in new ulong[] { 9, 2, 5, 0, 7 }) {
            dump.Add(id, new FonCollection { { "id", (int)id } });
        }
        Assert.True(dump.Remove(5));

        Assert.Equal(new ulong[] { 0, 2, 7, 9 }, dump.Ids);
        Assert.Equal(4, dump.Count);
        Assert.Equal(new ulong[] { 0, 2, 7, 9 }, dump.Select(pair => pair.Key).ToArray());
    }


    [Fact]
    public void DumpEnumeration_YieldsViewsWithTheirIds() {
        using var dump = new FonDump();
        dump.Add(4, new FonCollection { { "v", 40 } });
        dump.Add(1, new FonCollection { { "v", 10 } });

        var pairs = dump.ToList();

        Assert.Equal(1UL, pairs[0].Key);
        Assert.Equal(10, pairs[0].Value.Get<int>("v"));
        Assert.Equal(4UL, pairs[1].Key);
        Assert.Equal(40, pairs[1].Value.Get<int>("v"));
    }


    [Fact]
    public void EnumeratingHundredThousandRecords_FinishesWellUnderTwoSeconds() {
        var text = new StringBuilder();
        for (int i = 0; i < 100_000; i++) {
            text.Append("v=i:").Append(i).Append('\n');
        }
        using var dump = Fon.Deserialize(text.ToString());

        var stopwatch = Stopwatch.StartNew();
        long sum = 0;
        foreach (var pair in dump) {
            sum += pair.Value.Get<int>("v");
        }
        stopwatch.Stop();

        Assert.Equal(100_000, dump.Count);
        Assert.Equal(99_999L * 100_000L / 2L, sum);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Enumeration took {stopwatch.ElapsedMilliseconds} ms");
    }
}
