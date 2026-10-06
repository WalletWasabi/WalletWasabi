using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class RuntimeForegroundStateTests
{
    [Fact]
    public void ReturningBeforeQueuedStopExecutesRevokesTheStop()
    {
        var state = new RuntimeForegroundState { IsForeground = true };
        var runtime = new object();
        state.IsForeground = false;
        var queued = state.CaptureIdleStop(runtime)!;
        state.IsForeground = true;
        Assert.False(state.IsCurrent(queued, runtime));
    }

    [Fact]
    public void ASecondBackgroundPeriodRequiresANewDecision()
    {
        var state = new RuntimeForegroundState();
        var runtime = new object();
        var obsolete = state.CaptureIdleStop(runtime)!;
        state.IsForeground = true;
        state.IsForeground = false;
        Assert.False(state.IsCurrent(obsolete, runtime));
        Assert.True(state.IsCurrent(state.CaptureIdleStop(runtime)!, runtime));
    }

    [Fact]
    public void AQueuedStopCannotRetireAReplacementRuntime()
    {
        var state = new RuntimeForegroundState();
        var original = new object();
        var queued = state.CaptureIdleStop(original)!;
        Assert.False(state.IsCurrent(queued, new object()));
        Assert.False(state.IsCurrent(queued, null));
        Assert.True(state.IsCurrent(queued, original));
    }

    [Fact]
    public void ForegroundOrAbsentRuntimeNeverSchedulesAnIdleStop()
    {
        var state = new RuntimeForegroundState { IsForeground = true };
        Assert.Null(state.CaptureIdleStop(new object()));
        state.IsForeground = false;
        Assert.Null(state.CaptureIdleStop(null));
    }
}
