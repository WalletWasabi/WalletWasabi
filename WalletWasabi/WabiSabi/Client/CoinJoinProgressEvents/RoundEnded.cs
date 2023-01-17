using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class RoundEnded : CoinJoinProgressEventArgs
{
	public RoundEnded(RoundState lastRoundState)
	{
		LastRoundState = lastRoundState;
	}

	public RoundState LastRoundState { get; }
	public bool IsStopped { get; set; }
}
