namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class WaitingForRound : CoinJoinProgressEventArgs
{
	public WaitingForRound(bool isInCriticalPhase) : base(isInCriticalPhase)
	{
	}
}
