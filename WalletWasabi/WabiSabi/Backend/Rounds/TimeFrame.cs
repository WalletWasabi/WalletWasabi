namespace WalletWasabi.WabiSabi.Backend.Rounds;

public record TimeFrame
{
	private TimeFrame(DateTimeOffset startTime, TimeSpan duration)
	{
		StartTime = startTime;
		Duration = duration;
	}

	public static TimeFrame Create(TimeSpan duration) =>
		new(DateTimeOffset.MinValue, duration);

	public DateTimeOffset EndTime => StartTime + Duration;
	public DateTimeOffset StartTime { get; init; }
	public TimeSpan Duration { get; init; }
	public bool HasStarted => StartTime > DateTimeOffset.MinValue && StartTime < DateTimeOffset.UtcNow;
	public bool HasExpired => HasStarted && EndTime < DateTimeOffset.UtcNow;

	public TimeFrame StartNow() => this with { StartTime = DateTimeOffset.UtcNow };
}
