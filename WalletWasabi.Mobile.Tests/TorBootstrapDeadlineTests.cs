using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class TorBootstrapDeadlineTests
{
	[Fact]
	public void ColdDescriptorAndCircuitProgressCanExceedThreeMinutes()
	{
		var clock = new MonotonicClock();
		var deadline = new TorBootstrapDeadline(clock);
		deadline.CheckProgress(0);
		clock.Advance(TimeSpan.FromSeconds(52));
		deadline.CheckProgress(50);
		clock.Advance(TimeSpan.FromSeconds(20));
		deadline.CheckProgress(73);
		clock.Advance(TimeSpan.FromSeconds(113));
		deadline.CheckProgress(73); // The former absolute three-minute cutoff.
		deadline.CheckProgress(80);
		clock.Advance(TimeSpan.FromSeconds(60));
		deadline.CheckProgress(100);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(73)]
	public void StalledBootstrapRemainsBounded(int progress)
	{
		var clock = new MonotonicClock();
		var deadline = new TorBootstrapDeadline(clock);
		deadline.CheckProgress(progress);
		clock.Advance(TimeSpan.FromMinutes(3));
		Assert.Throws<TimeoutException>(() => deadline.CheckProgress(progress));
	}

	[Fact]
	public void RepeatedOrRegressedNoticesDoNotExtendIdleDeadline()
	{
		var clock = new MonotonicClock();
		var deadline = new TorBootstrapDeadline(clock);
		deadline.CheckProgress(73);
		clock.Advance(TimeSpan.FromMinutes(2));
		deadline.CheckProgress(50);
		clock.Advance(TimeSpan.FromMinutes(1));
		Assert.Throws<TimeoutException>(() => deadline.CheckProgress(73));
	}

	[Fact]
	public void ContinuousProgressCannotExtendStartupIndefinitely()
	{
		var clock = new MonotonicClock();
		var deadline = new TorBootstrapDeadline(clock);
		for (var minute = 1; minute < 10; minute++)
		{
			clock.Advance(TimeSpan.FromMinutes(1));
			deadline.CheckProgress(minute);
		}
		clock.Advance(TimeSpan.FromMinutes(1));
		Assert.Throws<TimeoutException>(() => deadline.CheckProgress(10));
	}

	private sealed class MonotonicClock : TimeProvider
	{
		private long _timestamp;
		public override long TimestampFrequency => TimeSpan.TicksPerSecond;
		public override long GetTimestamp() => _timestamp;
		public void Advance(TimeSpan time) => _timestamp += time.Ticks;
	}
}
