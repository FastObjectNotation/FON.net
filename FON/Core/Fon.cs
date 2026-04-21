using FON.Types;

namespace FON.Core;


public partial class Fon {
    public static bool DeserializeRawUnpack { get; set; } = false;


    private static int maxDepth = 64;

    /// <summary>
    /// Maximum number of nested brackets ({} or []) the parser will accept
    /// before rejecting input. Must be at least 1. Default: 64.
    /// </summary>
    public static int MaxDepth {
        get => maxDepth;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxDepth must be at least 1");
            }
            maxDepth = value;
        }
    }


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
        { typeof(RawData),      'r' }
    };




    private static char? GetTypeShort(Type type) {
        if (!SupportTypes.ContainsKey(type)) {
            return null;
        }
        return SupportTypes[type];
    }




    private static Type? GetType(char type) {
        if (!SupportTypes.ContainsValue(type)) {
            return null;
        }
        return SupportTypes.FirstOrDefault(x => x.Value == type).Key;
    }
}
