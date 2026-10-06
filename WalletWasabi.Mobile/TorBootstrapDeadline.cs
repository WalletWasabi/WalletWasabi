namespace WalletWasabi.Mobile;

/// <summary>Bounds cold Tor startup while allowing descriptor and circuit progress.</summary>
public sealed class TorBootstrapDeadline(TimeProvider? timeProvider = null)
{
	private static readonly TimeSpan MaximumStartup = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan MaximumIdle = TimeSpan.FromMinutes(3);
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
	private readonly long _started = (timeProvider ?? TimeProvider.System).GetTimestamp();
	private long _lastProgress = (timeProvider ?? TimeProvider.System).GetTimestamp();
	private int _highestProgress;

	public void CheckProgress(int progress)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(progress);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(progress, 100);
		var now = _timeProvider.GetTimestamp();
		if (progress > _highestProgress)
		{
			_highestProgress = progress;
			_lastProgress = now;
		}
		if (_timeProvider.GetElapsedTime(_started, now) >= MaximumStartup ||
			_timeProvider.GetElapsedTime(_lastProgress, now) >= MaximumIdle)
		{
			throw new TimeoutException("Tor bootstrap made no progress or exceeded its startup deadline.");
		}
	}
}
