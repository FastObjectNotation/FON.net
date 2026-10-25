using FON.Core;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// String handling: no truncation at buffer sizes, no replacement characters, embedded NUL,
/// long keys, escapes and metacharacters. Each value is checked through the API and through text.
/// </summary>
public class StringBindingTests {
    [Theory]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(100000)]
    public void AsciiScalar_OfBoundaryLength_IsNotTruncated(int length) {
        string value = new string('a', length);

        using var collection = new FonCollection { { "s", value } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(value, collection.Get<string>("s"));
        Assert.Equal(value, loaded.Get<string>("s"));
    }


    [Theory]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(100000)]
    public void MiddleItemOfStringArray_OfBoundaryLength_IsNotTruncated(int length) {
        string middle = new string('b', length);
        var items = new List<string> { "x", middle, "y" };

        using var collection = new FonCollection { { "arr", items } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(items, collection.Get<List<string>>("arr"));
        Assert.Equal(items, loaded.Get<List<string>>("arr"));
    }


    [Fact]
    public void CyrillicText_5000Characters_IsIntactWithoutReplacementCharacters() {
        string value = string.Concat(Enumerable.Repeat("ж", 5000));

        using var collection = new FonCollection { { "s", value } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(value, loaded.Get<string>("s"));
        Assert.DoesNotContain('\uFFFD', loaded.Get<string>("s"));
    }


    [Fact]
    public void SurrogatePairs_2500Emoji_AreIntactWithoutReplacementCharacters() {
        string value = string.Concat(Enumerable.Repeat("😀", 2500));

        using var collection = new FonCollection { { "s", value } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(value, loaded.Get<string>("s"));
        Assert.DoesNotContain('\uFFFD', loaded.Get<string>("s"));
    }


    [Fact]
    public void EmbeddedNul_InValue_RoundTripsThroughApiAndText() {
        string value = "left\0right";

        using var collection = new FonCollection { { "v", value } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(value, collection.Get<string>("v"));
        Assert.Equal(value, loaded.Get<string>("v"));
    }


    [Fact]
    public void EmbeddedNul_InKey_RoundTripsThroughApiAndText() {
        string key = "a\0b";

        using var collection = new FonCollection { { key, "value" } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(key, collection.Keys[0]);
        Assert.Equal("value", collection.Get<string>(key));
        Assert.Equal(key, loaded.Keys[0]);
        Assert.Equal("value", loaded.Get<string>(key));
    }


    [Fact]
    public void KeyLongerThan512Bytes_IsEnumeratedAndReadBack() {
        string key = new string('k', 600);

        using var collection = new FonCollection { { key, 42 } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(key, collection.Keys[0]);
        Assert.Equal(42, collection.Get<int>(key));
        Assert.Equal(key, loaded.Keys[0]);
        Assert.Equal(42, loaded.Get<int>(key));
    }


    [Fact]
    public void ValueEndingWithBackslash_RoundTrips() {
        string value = "C:\\dir\\";

        using var collection = new FonCollection { { "path", value } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(value, loaded.Get<string>("path"));
    }


    [Fact]
    public void ControlCharacters_RoundTrip() {
        string value = "\x01 start \x1b end \t\n\r\b\f";

        using var collection = new FonCollection { { "ctl", value } };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        Assert.Equal(value, loaded.Get<string>("ctl"));
    }


    [Fact]
    public void QuotesCommasBracketsAndBraces_InsideNestedObjects_RoundTrip() {
        string payload = "a,b]c}d{e\"f[g";
        string[] items = { "x,]", "}{", "\"q\"" };

        using var collection = new FonCollection {
            {
                "outer", new FonCollection {
                    { "text", payload },
                    { "list", new List<string>(items) },
                    { "inner", new FonCollection { { "deep", payload } } }
                }
            },
            {
                "rows", new List<FonCollection> {
                    new FonCollection { { "s", payload } }
                }
            }
        };
        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(collection));

        var outer = loaded.Get<FonCollection>("outer");
        Assert.Equal(payload, outer.Get<string>("text"));
        Assert.Equal(items, outer.Get<List<string>>("list"));
        Assert.Equal(payload, outer.Get<FonCollection>("inner").Get<string>("deep"));
        Assert.Equal(payload, loaded.Get<List<FonCollection>>("rows")[0].Get<string>("s"));
    }
}
