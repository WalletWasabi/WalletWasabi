namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class CoinJoinProgressEventArgs : EventArgs
{
	public bool IsInCriticalPhase { get; protected set; }
}
