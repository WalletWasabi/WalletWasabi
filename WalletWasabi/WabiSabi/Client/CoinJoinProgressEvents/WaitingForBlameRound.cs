namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class WaitingForBlameRound : CoinJoinProgressEventArgs
{
	public WaitingForBlameRound(DateTimeOffset timeoutAt, bool isInCriticalPhase) : base(isInCriticalPhase)
	{
		TimeoutAt = timeoutAt;
	}

	public DateTimeOffset TimeoutAt { get; }
}
