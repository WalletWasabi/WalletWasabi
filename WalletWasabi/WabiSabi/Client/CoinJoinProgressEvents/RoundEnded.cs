using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class RoundEnded : CoinJoinProgressEventArgs
{
	public RoundEnded(RoundState roundState, bool isInCriticalPhase) : base(isInCriticalPhase)
	{
		RoundState = roundState;
	}

	public RoundState RoundState { get; }
}
