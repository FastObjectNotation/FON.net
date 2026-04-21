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
}
