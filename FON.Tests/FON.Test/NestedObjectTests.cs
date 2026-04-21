using FON.Core;
using FON.Types;

namespace FON.Test;


public class NestedObjectTests {
    [Fact]
    public void MaxDepth_DefaultsTo64() {
        Assert.Equal(64, Fon.MaxDepth);
    }


    [Fact]
    public void MaxDepth_RejectsZero() {
        var original = Fon.MaxDepth;
        try {
            Assert.Throws<ArgumentOutOfRangeException>(() => Fon.MaxDepth = 0);
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public void MaxDepth_RejectsNegative() {
        var original = Fon.MaxDepth;
        try {
            Assert.Throws<ArgumentOutOfRangeException>(() => Fon.MaxDepth = -1);
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public void MaxDepth_AcceptsOne() {
        var original = Fon.MaxDepth;
        try {
            Fon.MaxDepth = 1;
            Assert.Equal(1, Fon.MaxDepth);
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public void SupportTypes_RegistersFonCollectionAsObject() {
        Assert.True(Fon.SupportTypes.ContainsKey(typeof(FonCollection)));
        Assert.Equal('o', Fon.SupportTypes[typeof(FonCollection)]);
    }


    [Fact]
    public void Serialize_NestedObject_ProducesObjectLiteral() {
        var inner = new FonCollection { { "x", 1 } };
        var outer = new FonCollection { { "inner", inner } };

        var text = Fon.SerializeToString(outer);

        Assert.Equal("inner=o:{x=i:1}", text);
    }


    [Fact]
    public void Serialize_TwoLevelNested_ProducesNestedLiteral() {
        var deepest = new FonCollection { { "v", 99 } };
        var middle = new FonCollection { { "deep", deepest } };
        var outer = new FonCollection { { "mid", middle } };

        var text = Fon.SerializeToString(outer);

        Assert.Equal("mid=o:{deep=o:{v=i:99}}", text);
    }


    [Fact]
    public void Serialize_EmptyNestedObject_ProducesEmptyBraces() {
        var outer = new FonCollection { { "empty", new FonCollection() } };

        var text = Fon.SerializeToString(outer);

        Assert.Equal("empty=o:{}", text);
    }


    [Fact]
    public void Deserialize_NestedObject_RoundTrips() {
        var inner = new FonCollection { { "x", 1 } };
        var outer = new FonCollection { { "inner", inner } };

        var text = Fon.SerializeToString(outer);
        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            Assert.Equal(1, loaded.Count);
            var loadedOuter = loaded[0];
            var loadedInner = loadedOuter.Get<FonCollection>("inner");
            Assert.Equal(1, loadedInner.Get<int>("x"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void Deserialize_ThreeLevelNested_RoundTrips() {
        var l3 = new FonCollection { { "v", 42 } };
        var l2 = new FonCollection { { "l3", l3 } };
        var l1 = new FonCollection { { "l2", l2 } };
        var l0 = new FonCollection { { "l1", l1 } };

        var dump = new FonDump();
        dump.TryAdd(0, l0);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var got = loaded[0]
                .Get<FonCollection>("l1")
                .Get<FonCollection>("l2")
                .Get<FonCollection>("l3")
                .Get<int>("v");
            Assert.Equal(42, got);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void Deserialize_EmptyNestedObject_RoundTrips() {
        var outer = new FonCollection { { "empty", new FonCollection() } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var inner = loaded[0].Get<FonCollection>("empty");
            Assert.Equal(0, inner.Count());
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task Deserialize_NestedObject_NotFollowedByBrace_Throws() {
        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            File.WriteAllText(tempFile.FullName, "x=o:42\n");
            var ex = await Assert.ThrowsAsync<AggregateException>(() => Fon.DeserializeFromFileAutoAsync(tempFile));
            Assert.IsType<FormatException>(ex.InnerException);
        } finally {
            tempFile.Delete();
        }
    }
}
