using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Tests for native library availability and basic functionality. FON.net is a fully
/// native wrapper — there is no managed fallback, so these checks are load-bearing:
/// if the native library isn't present, nothing else in the package works.
/// </summary>
public class NativeAvailabilityTests {
    [Fact]
    public void NativeLibrary_IsAvailable() {
        Assert.True(NativeLoader.IsAvailable, "Native library should be available for these tests");
    }


    [Fact]
    public void NativeLibrary_HasVersion() {
        var version = NativeLoader.GetVersion();
        Assert.Equal("0.3.0", version);
    }
}
