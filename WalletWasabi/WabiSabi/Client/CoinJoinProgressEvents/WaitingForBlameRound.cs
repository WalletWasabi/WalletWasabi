namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class WaitingForBlameRound : CoinJoinProgressEventArgs
{
	public WaitingForBlameRound(DateTimeOffset dateTimeOffset)
	{
		DateTimeOffset = dateTimeOffset;
	}

	public DateTimeOffset DateTimeOffset { get; }
}
