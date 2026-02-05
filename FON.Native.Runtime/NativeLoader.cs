using System.Runtime.InteropServices;

namespace FON.Native;


/// <summary>
/// Helper for checking native library availability
/// </summary>
public static class NativeLoader {
    private static readonly Lazy<bool> _isAvailable = new(() => {
        try {
            return NativeBindings.fon_version() != IntPtr.Zero;
        } catch {
            return false;
        }
    });

    /// <summary>
    /// Check if native library is available
    /// </summary>
    public static bool IsAvailable => _isAvailable.Value;



    /// <summary>
    /// Get native library version
    /// </summary>
    public static string? GetVersion() {
        if (!IsAvailable) {
            return null;
        }

        try {
            var ptr = NativeBindings.fon_version();
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        } catch {
            return null;
        }
    }



    /// <summary>
    /// Get current runtime identifier
    /// </summary>
    public static string GetRuntimeIdentifier() {
        var arch = RuntimeInformation.ProcessArchitecture switch {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "unknown"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return $"win-{arch}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return $"linux-{arch}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return $"osx-{arch}";
        }

        return $"unknown-{arch}";
    }
}
