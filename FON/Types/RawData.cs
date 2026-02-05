using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace FON.Types;


public class RawData : IDisposable {
    private static ReadOnlySpan<char> Z85Encode => "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

    private static ReadOnlySpan<byte> Z85Decode => [
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, // 0-15
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, // 16-31
        255, 68,  255, 84,  83,  82,  72,  255, 75,  76,  70,  65,  255, 63,  62,  69,  // 32-47: ! # $ % & ( ) * + - . /
        0,   1,   2,   3,   4,   5,   6,   7,   8,   9,  64,  255, 73,  66,  74,  71,   // 48-63: 0-9 : < = > ?
        81,  36,  37,  38,  39,  40,  41,  42,  43,  44,  45,  46,  47,  48,  49,  50,  // 64-79: @ A-O
        51,  52,  53,  54,  55,  56,  57,  58,  59,  60,  61,  77,  255, 78,  67,  255, // 80-95: P-Z [ ] ^
        255, 10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23,  24,  // 96-111: a-o
        25,  26,  27,  28,  29,  30,  31,  32,  33,  34,  35,  79,  255, 80,  255, 255  // 112-127: p-z { }
    ];

    public string encoded { get; private set; } = string.Empty;
    public byte[] data { get; private set; } = [];


    public RawData(byte[] data) => this.data = data;

    public RawData(ReadOnlySpan<byte> data) => this.data = data.ToArray();

    public RawData(Span<byte> data) => this.data = data.ToArray();

    public RawData(ReadOnlySpan<char> encoded) => this.encoded = encoded.ToString();

    public RawData(Span<char> encoded) => this.encoded = encoded.ToString();


    public void Dispose() {
        data = [];
        encoded = string.Empty;
    }


    public static RawData Create(FileInfo file) => new(File.ReadAllBytes(file.FullName));

    public static RawData Create(string data) => new(Encoding.UTF8.GetBytes(data));



    public RawData Unpack() {
        if (encoded == string.Empty) {
            return this;
        }

        // Z85: 5 chars -> 4 bytes, with possible padding
        var paddingInfo = GetPaddingInfo(encoded);
        int decodedLength = (encoded.Length / 5 * 4) - paddingInfo.removedBytes;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(decodedLength + 4);
        try {
            int written = Z85DecodeBytes(encoded.AsSpan(), buffer, paddingInfo);
            data = buffer[..written];
            encoded = string.Empty;
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return this;
    }



    public RawData Pack() {
        if (encoded != string.Empty) {
            return this;
        }

        // Z85 requires input length divisible by 4, we pad if needed
        int padding = (4 - (data.Length % 4)) % 4;
        int paddedLength = data.Length + padding;
        int outputLength = paddedLength / 4 * 5;

        char[] chars = ArrayPool<char>.Shared.Rent(outputLength + 1); // +1 for padding marker

        try {
            int written = Z85EncodeBytes(data, chars, padding);
            encoded = new string(chars, 0, written);
            data = [];
        } finally {
            ArrayPool<char>.Shared.Return(chars);
        }

        return this;
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Z85EncodeBytes(ReadOnlySpan<byte> input, Span<char> output, int padding) {
        var alphabet = Z85Encode;
        int inputLen = input.Length;
        int paddedLen = inputLen + padding;
        int writePos = 0;

        // Process full 4-byte blocks
        int fullBlocks = inputLen / 4;
        for (int i = 0; i < fullBlocks; i++) {
            int offset = i * 4;
            uint value = ((uint)input[offset] << 24)        |
                         ((uint)input[offset + 1] << 16)    |
                         ((uint)input[offset + 2] << 8)     |
                         input[offset + 3];

            output[writePos + 4] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos + 3] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos + 2] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos + 1] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos] = alphabet[(int)value];
            writePos += 5;
        }

        // Handle remaining bytes with padding
        int remaining = inputLen % 4;
        if (remaining > 0) {
            uint value = 0;
            int offset = fullBlocks * 4;

            for (int i = 0; i < remaining; i++) {
                value = (value << 8) | input[offset + i];
            }
            // Pad with zeros
            for (int i = remaining; i < 4; i++) {
                value <<= 8;
            }

            output[writePos + 4] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos + 3] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos + 2] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos + 1] = alphabet[(int)(value % 85)]; value /= 85;
            output[writePos] = alphabet[(int)value];
            writePos += 5;

            // Append padding marker (number of padding bytes: 1, 2, or 3)
            output[writePos] = (char)('0' + padding);
            writePos++;
        }

        return writePos;
    }



    private static (int removedBytes, bool hasPadding) GetPaddingInfo(string encoded) {
        if (encoded.Length == 0) {
            return (0, false);
        }

        // Check if last char is a padding marker (1, 2, or 3)
        char last = encoded[^1];
        if (last >= '1' && last <= '3') {
            return (last - '0', true);
        }
        return (0, false);
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Z85DecodeBytes(ReadOnlySpan<char> input, Span<byte> output, (int removedBytes, bool hasPadding) paddingInfo) {
        var decode = Z85Decode;
        int inputLen = paddingInfo.hasPadding ? input.Length - 1 : input.Length;
        int writePos = 0;

        for (int i = 0; i < inputLen; i += 5) {
            uint value = 0;
            for (int j = 0; j < 5; j++) {
                char c = input[i + j];
                if (c > 127) {
                    throw new FormatException($"Invalid Z85 character at position {i + j}");
                }
                byte decoded = decode[c];
                if (decoded == 255) {
                    throw new FormatException($"Invalid Z85 character '{c}' at position {i + j}");
                }
                value = value * 85 + decoded;
            }

            output[writePos] = (byte)(value >> 24);
            output[writePos + 1] = (byte)(value >> 16);
            output[writePos + 2] = (byte)(value >> 8);
            output[writePos + 3] = (byte)value;
            writePos += 4;
        }

        // Remove padding bytes from the end
        return writePos - paddingInfo.removedBytes;
    }
}
