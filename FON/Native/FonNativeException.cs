namespace FON.Native;


/// <summary>
/// Thrown when the native library reports a failure that has no more specific .NET mapping:
/// a parse error, a write error or a file that cannot be read. Argument errors are thrown as
/// <see cref="ArgumentException"/>.
/// </summary>
public class FonNativeException : Exception {
    public FonNativeException(int code, string message) : base(message) {
        Code = code;
    }


    /// <summary>The <see cref="FonResultCode"/> reported by the native library.</summary>
    public int Code { get; }
}



/// <summary>
/// Thrown when a view is used after the data it refers to was changed or removed: a record
/// was replaced or removed, a field of its record changed, or the root collection was mutated.
/// Obtain the value again from its owner.
/// </summary>
public sealed class FonStaleViewException : FonNativeException {
    public FonStaleViewException(string message) : base(FonResultCode.StaleView, message) {
    }
}
