namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class WaitingForBlameRound : CoinJoinProgressEventArgs
{
	public WaitingForBlameRound(DateTimeOffset timeoutAt)
	{
		TimeoutAt = timeoutAt;
	}

	public DateTimeOffset TimeoutAt { get; }
}
