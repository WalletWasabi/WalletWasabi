using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class EnteringSigningPhase : RoundStateChanged
{
	public EnteringSigningPhase(RoundState roundState, DateTimeOffset timeoutAt, bool isInCriticalPhase) : base(roundState, timeoutAt, isInCriticalPhase)
	{
	}
}
