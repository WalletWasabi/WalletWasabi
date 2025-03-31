namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public record TimeFrame
{
	public TimeFrame(DateTimeOffset startTime, TimeSpan duration)
	{
		StartTime = startTime;
		Duration = duration;
	}

	public static readonly TimeFrame Zero = Create(TimeSpan.Zero);

	public static TimeFrame Create(TimeSpan duration) =>
		new(DateTimeOffset.MinValue, duration);

	public DateTimeOffset EndTime => StartTime + Duration;
	public DateTimeOffset StartTime { get; init; }
	public TimeSpan Duration { get; init; }
	public TimeSpan Remaining => EndTime - DateTimeOffset.UtcNow;
	public bool HasStarted => StartTime > DateTimeOffset.MinValue && StartTime < DateTimeOffset.UtcNow;
	public bool HasExpired => HasStarted && EndTime < DateTimeOffset.UtcNow;

	public TimeFrame StartNow() => this with { StartTime = DateTimeOffset.UtcNow };

	public bool Includes(DateTimeOffset dateTime) =>
		dateTime >= StartTime && dateTime < EndTime;
}
