namespace FON.Acceleration;


/// <summary>
/// Manages native acceleration for FON serialization.
/// Automatically detects and uses native library if available.
/// </summary>
public static class FonAccelerator {
    private static bool? _isAvailable;
    private static string? _version;


    /// <summary>
    /// Check if native acceleration is available.
    /// </summary>
    public static bool IsAvailable {
        get {
            _isAvailable ??= CheckNativeAvailability();
            return _isAvailable.Value;
        }
    }


    /// <summary>
    /// Native library version (null if not available).
    /// </summary>
    public static string? Version {
        get {
            if (!IsAvailable) {
                return null;
            }
            _version ??= GetNativeVersion();
            return _version;
        }
    }


    /// <summary>
    /// Force disable native acceleration (useful for testing/debugging).
    /// </summary>
    public static bool ForceManaged { get; set; } = false;


    /// <summary>
    /// Returns true if native acceleration should be used.
    /// </summary>
    internal static bool ShouldUseNative => !ForceManaged && IsAvailable;



    private static bool CheckNativeAvailability() {
        try {
            var runtimeAssembly = System.Reflection.Assembly.Load("FON.Native.Runtime");
            if (runtimeAssembly == null) {
                return false;
            }

            var loaderType = runtimeAssembly.GetType("FON.Native.NativeLoader");
            if (loaderType == null) {
                return false;
            }

            var isAvailableProp = loaderType.GetProperty("IsAvailable");
            if (isAvailableProp == null) {
                return false;
            }

            return (bool)(isAvailableProp.GetValue(null) ?? false);
        } catch {
            return false;
        }
    }



    private static string? GetNativeVersion() {
        try {
            var runtimeAssembly = System.Reflection.Assembly.Load("FON.Native.Runtime");
            var loaderType = runtimeAssembly?.GetType("FON.Native.NativeLoader");
            var getVersionMethod = loaderType?.GetMethod("GetVersion");
            return getVersionMethod?.Invoke(null, null) as string;
        } catch {
            return null;
        }
    }
}
