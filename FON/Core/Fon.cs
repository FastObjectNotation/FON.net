using FON.Native;
using FON.Types;

namespace FON.Core;


public partial class Fon {
    private static bool deserializeRawUnpack = false;

    /// <summary>
    /// Decode raw values from Z85 while parsing instead of on first read. The values read
    /// back are the same either way; with this on, invalid Z85 text fails the parse instead
    /// of the later read.
    /// </summary>
    public static bool DeserializeRawUnpack {
        get => deserializeRawUnpack;
        set {
            deserializeRawUnpack = value;
            NativeBindings.fon_set_raw_unpack(value ? 1 : 0);
        }
    }


    private static int maxDepth = 64;

    /// <summary>
    /// Maximum number of nested brackets ({} or []) the parser will accept
    /// before rejecting input. Must be at least 1. Default: 64. Values above 128
    /// act as 128.
    /// </summary>
    public static int MaxDepth {
        get => maxDepth;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxDepth must be at least 1");
            }
            maxDepth = value;
            NativeBindings.fon_set_max_depth(value);
        }
    }


    /// <summary>
    /// Threshold historically used to choose between managed serialization strategies.
    /// Retained for source compatibility — every SerializeToFile*/DeserializeFromFile*
    /// method now routes through the same native implementation regardless of size.
    /// </summary>
    public static int ParallelMethodThreshold { get; set; } = 2000;


    public static readonly Dictionary<Type, char> SupportTypes = new() {
        { typeof(byte),         'e' },
        { typeof(short),        't' },
        { typeof(int),          'i' },
        { typeof(uint),         'u' },
        { typeof(long),         'l' },
        { typeof(ulong),        'g' },
        { typeof(float),        'f' },
        { typeof(double),       'd' },
        { typeof(string),       's' },
        { typeof(bool),         'b' },
        { typeof(RawData),      'r' },
        { typeof(FonCollection),'o' }
    };


    internal static long EstimateBytes(long inputByteLength) => Math.Max(64, inputByteLength);
}
