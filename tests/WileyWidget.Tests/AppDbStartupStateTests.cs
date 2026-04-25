using WileyWidget.Data;

namespace WileyWidget.Tests;

public sealed class AppDbStartupStateTests
{
    [Fact]
    public void StartupState_TracksInitializationFallbackAndReset()
    {
        AppDbStartupState.ResetForTests();

        Assert.False(AppDbStartupState.InitializationAttempted);
        Assert.False(AppDbStartupState.FallbackActivated);
        Assert.False(AppDbStartupState.IsDegradedMode);
        Assert.Null(AppDbStartupState.FallbackReason);

        AppDbStartupState.MarkInitializationAttempted();
        AppDbStartupState.MarkInitializationAttempted();
        AppDbStartupState.ActivateFallback("primary database unavailable");
        AppDbStartupState.ActivateFallback("secondary reason should be ignored");

        Assert.True(AppDbStartupState.InitializationAttempted);
        Assert.True(AppDbStartupState.FallbackActivated);
        Assert.True(AppDbStartupState.IsDegradedMode);
        Assert.Equal("primary database unavailable", AppDbStartupState.FallbackReason);

        AppDbStartupState.ResetForTests();

        Assert.False(AppDbStartupState.InitializationAttempted);
        Assert.False(AppDbStartupState.FallbackActivated);
        Assert.False(AppDbStartupState.IsDegradedMode);
        Assert.Null(AppDbStartupState.FallbackReason);
    }
}