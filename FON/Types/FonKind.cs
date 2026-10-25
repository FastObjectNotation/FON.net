using FON.Native;

namespace FON.Types;


/// <summary>
/// Type of a value stored in a <see cref="FonCollection"/>. Array kinds report the element type.
/// </summary>
public enum FonType {
    Byte,
    Short,
    Int,
    UInt,
    Long,
    ULong,
    Float,
    Double,
    Bool,
    String,
    Raw,
    Object
}



/// <summary>
/// Kind of a value: its <see cref="FonType"/> and whether it is an array of that type.
/// </summary>
public readonly record struct FonKind(FonType Type, bool IsArray);



internal static class FonKindCodes {
    internal static FonType ToType(char code) {
        return code switch {
            FonTypeCode.Byte => FonType.Byte,
            FonTypeCode.Short => FonType.Short,
            FonTypeCode.Int => FonType.Int,
            FonTypeCode.UInt => FonType.UInt,
            FonTypeCode.Long => FonType.Long,
            FonTypeCode.ULong => FonType.ULong,
            FonTypeCode.Float => FonType.Float,
            FonTypeCode.Double => FonType.Double,
            FonTypeCode.Bool => FonType.Bool,
            FonTypeCode.String => FonType.String,
            FonTypeCode.Raw => FonType.Raw,
            FonTypeCode.Object => FonType.Object,
            _ => throw new NotSupportedException($"Unknown native type tag '{code}'")
        };
    }
}
