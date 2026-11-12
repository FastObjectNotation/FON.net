using FON.Core;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// The managed Z85 codec of <see cref="RawData"/>: every length, groups that end in a padding
/// digit, malformed text, and agreement with the text the library itself writes.
/// </summary>
public class RawDataZ85Tests {
    private static byte[] Sample(int length) {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++) {
            bytes[i] = (byte)((i * 97 + 13) % 256);
        }
        return bytes;
    }


    [Fact]
    public void EveryLength_PacksAndUnpacks() {
        for (int length = 1; length <= 64; length++) {
            byte[] bytes = Sample(length);
            string text = new RawData(bytes).Pack().encoded;

            Assert.Equal(bytes, new RawData(text.AsSpan()).Unpack().data);
        }
    }


    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(86u)]
    [InlineData(7226u)]
    [InlineData(4294967211u)]
    public void FullGroupEndingInAPaddingDigit_RoundTrips(uint value) {
        byte[] bytes = [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        string text = new RawData(bytes).Pack().encoded;

        Assert.Equal(5, text.Length);
        Assert.InRange(text[4], '1', '3');
        Assert.Equal(bytes, new RawData(text.AsSpan()).Unpack().data);
    }


    [Theory]
    [InlineData("1")]
    [InlineData("abc")]
    [InlineData("000000")]
    [InlineData("0000004")]
    [InlineData("000000000")]
    [InlineData("#####")]
    [InlineData("0000\u00e9")]
    public void MalformedText_ThrowsFormatException(string text) {
        Assert.Throws<FormatException>(() => new RawData(text.AsSpan()).Unpack());
    }


    [Fact]
    public void ManagedEncoding_MatchesTheTextTheLibraryWrites() {
        for (int length = 1; length <= 40; length++) {
            byte[] bytes = Sample(length);
            using var collection = new FonCollection { { "r", new RawData(bytes) } };

            string expected = "r=r:\"" + new RawData(bytes).Pack().encoded + "\"";
            Assert.Equal(expected, Fon.SerializeToString(collection));
        }
    }
}
