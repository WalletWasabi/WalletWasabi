using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

internal class EnteringConnectionConfirmationPhase : RoundStateChanged
{
	public EnteringConnectionConfirmationPhase(RoundState roundState, DateTimeOffset timeoutAt) : base(roundState, timeoutAt)
	{
		IsInCriticalPhase = true;
	}
}
