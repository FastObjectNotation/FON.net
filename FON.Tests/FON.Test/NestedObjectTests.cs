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
}
