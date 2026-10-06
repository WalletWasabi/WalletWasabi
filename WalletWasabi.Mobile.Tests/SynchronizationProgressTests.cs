using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class SynchronizationProgressTests
{
    private static SynchronizationSnapshot Snapshot(uint headers = 100, uint? target = 1000, uint filters = 0,
        uint? wallet = null, bool loaded = false, bool synchronized = false) => new("Main", true, 100, 4, headers, target, filters, wallet, loaded, synchronized);

    [Fact]
    public void RemainingTimeRequiresMeasuredProgressAndExpiresWhenStalled()
    {
        var clock = new Clock();
        var tracker = new SynchronizationProgressTracker(clock);
        Assert.Null(tracker.Update(Snapshot()).EstimatedRemaining);
        clock.Advance(10);
        var advancing = tracker.Update(Snapshot(headers: 200));
        Assert.Equal(0.2, advancing.Fraction);
        Assert.Equal(TimeSpan.FromSeconds(80), advancing.EstimatedRemaining);
        clock.Advance(31);
        var stalled = tracker.Update(Snapshot(headers: 200));
        Assert.True(stalled.WaitingForData);
        Assert.Null(stalled.EstimatedRemaining);
        Assert.Equal(TimeSpan.FromSeconds(41), stalled.Elapsed);
    }

    [Fact]
    public void CachedFilterCheckpointIsTheBeginningOfFilterWork()
    {
        var tracker = new SynchronizationProgressTracker();
        tracker.Update(Snapshot(headers: 0, target: 970000, filters: 960000));
        var result = tracker.Update(Snapshot(headers: 970000, target: 970000, filters: 965000));
        Assert.Equal(SynchronizationStage.Filters, result.Stage);
        Assert.Equal(0.5, result.Fraction);
    }

    [Fact]
    public void WalletScanningHasItsOwnRateAndOnlyReadinessCompletesIt()
    {
        var clock = new Clock();
        var tracker = new SynchronizationProgressTracker(clock);
        var initial = tracker.Update(Snapshot(headers: 1000, filters: 1000, wallet: 900, loaded: true));
        Assert.Equal(SynchronizationStage.Wallet, initial.Stage);
        Assert.Equal(0, initial.Fraction);
        clock.Advance(10);
        var halfway = tracker.Update(Snapshot(headers: 1000, filters: 1000, wallet: 950, loaded: true));
        Assert.Equal(0.5, halfway.Fraction);
        Assert.Equal(TimeSpan.FromSeconds(10), halfway.EstimatedRemaining);
        Assert.Equal(SynchronizationStage.Ready, tracker.Update(Snapshot(headers: 1000, filters: 1000, wallet: 1000, loaded: true, synchronized: true)).Stage);
    }

    [Fact]
    public void UnknownTargetsAndDepartedPeersCannotProduceAnEstimate()
    {
        var tracker = new SynchronizationProgressTracker();
        var unknown = tracker.Update(Snapshot(target: null));
        Assert.Null(unknown.Fraction);
        Assert.Null(unknown.EstimatedRemaining);
        var departed = tracker.Update(Snapshot() with { Peers = 0 });
        Assert.Equal(SynchronizationStage.Peers, departed.Stage);
        Assert.Null(departed.EstimatedRemaining);
    }

    [Fact]
    public void TargetChangesAndReorganizationsDiscardObsoleteRates()
    {
        var clock = new Clock();
        var tracker = new SynchronizationProgressTracker(clock);
        tracker.Update(Snapshot());
        clock.Advance(10);
        Assert.NotNull(tracker.Update(Snapshot(headers: 200)).EstimatedRemaining);
        clock.Advance(1);
        Assert.Null(tracker.Update(Snapshot(headers: 210, target: 2000)).EstimatedRemaining);
        clock.Advance(10);
        Assert.NotNull(tracker.Update(Snapshot(headers: 300, target: 2000)).EstimatedRemaining);
        Assert.Null(tracker.Update(Snapshot(headers: 180, target: 2000)).EstimatedRemaining);
    }

    [Fact]
    public void TorProgressDoesNotPretendItsPhasesHaveEqualDuration()
    {
        var clock = new Clock();
        var tracker = new SynchronizationProgressTracker(clock);
        tracker.Update(Snapshot() with { TorBootstrap = 10, EngineReady = false });
        clock.Advance(30);
        var result = tracker.Update(Snapshot() with { TorBootstrap = 80, EngineReady = false });
        Assert.Equal(SynchronizationStage.Tor, result.Stage);
        Assert.Equal(0.8, result.Fraction);
        Assert.Null(result.EstimatedRemaining);
    }

    private sealed class Clock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(int seconds) => _timestamp += TimeSpan.FromSeconds(seconds).Ticks;
    }
}
