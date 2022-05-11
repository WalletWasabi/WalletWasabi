namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class CoinJoinProgressEventArgs : EventArgs
{
	public CoinJoinProgressEventArgs(bool isInCriticalPhase)
	{
		IsInCriticalPhase = isInCriticalPhase;
	}
	
	public bool IsInCriticalPhase { get; }
}
