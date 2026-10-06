namespace WalletWasabi.Mobile;

public enum SynchronizationStage { Tor, Starting, Peers, Headers, Filters, Wallet, Ready, Failed }

public sealed record SynchronizationSnapshot(string Network, bool EngineReady, int TorBootstrap, int Peers,
    uint Headers, uint? Target, uint Filters, uint? WalletHeight, bool WalletLoaded, bool Synchronized,
    string? WalletReference = null, string? Error = null);

public sealed record SynchronizationProgress(SynchronizationStage Stage, double? Fraction, uint Position,
    uint? Target, TimeSpan Elapsed, TimeSpan? EstimatedRemaining, bool WaitingForData, string? Error = null);

/// <summary>Measures the current synchronization stage using monotonic progress, never a guessed total duration.</summary>
public sealed class SynchronizationProgressTracker(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly List<(long Timestamp, uint Position)> _samples = [];
    private string? _scope;
    private SynchronizationStage? _stage;
    private uint? _filterStart;
    private uint? _walletStart;
    private uint _lastPosition;
    private uint? _lastTarget;
    private long _started;
    private long _lastProgress;

    public void Reset()
    {
        _scope = null;
        _stage = null;
        _filterStart = null;
        _walletStart = null;
        _samples.Clear();
    }

    public SynchronizationProgress Update(SynchronizationSnapshot snapshot)
    {
        var now = _clock.GetTimestamp();
        var scope = snapshot.Network + "/" + snapshot.WalletReference;
        if (_scope != scope)
        {
            Reset();
            _scope = scope;
            _started = now;
        }
        _filterStart = Math.Min(_filterStart ?? snapshot.Filters, snapshot.Filters);
        if (snapshot.WalletHeight is { } wallet) { _walletStart = Math.Min(_walletStart ?? wallet, wallet); }

        var stage = snapshot.Error is not null ? SynchronizationStage.Failed
            : snapshot.TorBootstrap < 100 ? SynchronizationStage.Tor
            : !snapshot.EngineReady ? SynchronizationStage.Starting
            : snapshot.Peers == 0 ? SynchronizationStage.Peers
            : snapshot.Target is null || snapshot.Headers < snapshot.Target ? SynchronizationStage.Headers
            : snapshot.Filters < snapshot.Target ? SynchronizationStage.Filters
            : snapshot.WalletHeight is not null && (!snapshot.WalletLoaded || !snapshot.Synchronized) ? SynchronizationStage.Wallet
            : SynchronizationStage.Ready;
        var position = stage switch
        {
            SynchronizationStage.Tor => (uint)Math.Clamp(snapshot.TorBootstrap, 0, 100),
            SynchronizationStage.Headers => snapshot.Headers,
            SynchronizationStage.Filters => snapshot.Filters,
            SynchronizationStage.Wallet => snapshot.WalletHeight ?? 0,
            _ => 0u
        };
        uint? target = stage == SynchronizationStage.Tor ? 100u
            : stage is SynchronizationStage.Headers or SynchronizationStage.Filters or SynchronizationStage.Wallet ? snapshot.Target : null;
        var start = stage == SynchronizationStage.Filters ? _filterStart ?? position
            : stage == SynchronizationStage.Wallet ? _walletStart ?? position : 0u;
        if (_stage != stage || position < _lastPosition || target != _lastTarget)
        {
            _samples.Clear();
            _lastProgress = now;
        }
        if (position > _lastPosition || _stage != stage) { _lastProgress = now; }
        _stage = stage;
        _lastPosition = position;
        _lastTarget = target;
        _samples.RemoveAll(sample => _clock.GetElapsedTime(sample.Timestamp, now) > TimeSpan.FromSeconds(45));
        if (_samples.Count == 0 || _samples[^1].Timestamp != now) { _samples.Add((now, position)); }

        var waiting = (stage is SynchronizationStage.Headers or SynchronizationStage.Filters or SynchronizationStage.Wallet)
            && _clock.GetElapsedTime(_lastProgress, now) >= TimeSpan.FromSeconds(30);
        TimeSpan? remaining = null;
        if (!waiting && (stage is SynchronizationStage.Headers or SynchronizationStage.Filters or SynchronizationStage.Wallet)
            && target is { } end && position < end && _samples.Count >= 2)
        {
            var first = _samples[0];
            var elapsed = _clock.GetElapsedTime(first.Timestamp, now).TotalSeconds;
            if (elapsed >= 10 && position > first.Position)
            {
                var seconds = (end - position) * elapsed / (position - first.Position);
                if (double.IsFinite(seconds) && seconds <= TimeSpan.FromDays(30).TotalSeconds)
                { remaining = TimeSpan.FromSeconds(Math.Max(1, seconds)); }
            }
        }
        double? fraction = target is { } total && total > start ? Math.Clamp(((double)position - start) / (total - start), 0, 1) : null;
        return new(stage, fraction, position, target, _clock.GetElapsedTime(_started, now), remaining, waiting, snapshot.Error);
    }
}
