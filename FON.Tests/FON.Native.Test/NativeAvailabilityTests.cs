using FON.Acceleration;
using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Tests for native library availability and basic functionality.
/// These tests require the native library to be compiled and available.
/// </summary>
public class NativeAvailabilityTests {
    [Fact]
    public void NativeLibrary_IsAvailable() {
        Assert.True(NativeLoader.IsAvailable, "Native library should be available for these tests");
    }


    [Fact]
    public void NativeLibrary_HasVersion() {
        var version = NativeLoader.GetVersion();
        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }


    [Fact]
    public void FonAccelerator_DetectsNative() {
        Assert.True(FonAccelerator.IsAvailable, "FonAccelerator should detect native library");
    }


    [Fact]
    public void FonAccelerator_ReportsVersion() {
        var version = FonAccelerator.Version;
        Assert.NotNull(version);
    }
}
