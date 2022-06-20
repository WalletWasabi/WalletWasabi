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
	public bool HasExpired(Phase phase)
	{
		TimeSpan bufferTime;
		if (phase == Phase.InputRegistration)
		{
			bufferTime = TimeSpan.FromMinutes(1);
		}
		else if (phase == Phase.ConnectionConfirmation)
		{
			bufferTime = TimeSpan.Zero;
		}
		else if (phase == Phase.OutputRegistration)
		{
			bufferTime = TimeSpan.FromMinutes(1);
		}
		else //if (phase == Phase.TransactionSigning)
		{
			// Clients those didn't update would miss the blame round completely.
			bufferTime = TimeSpan.Zero;
		}
		return HasStarted && (EndTime + bufferTime) < DateTimeOffset.UtcNow;
	}

	public TimeFrame StartNow() => this with { StartTime = DateTimeOffset.UtcNow };
}
