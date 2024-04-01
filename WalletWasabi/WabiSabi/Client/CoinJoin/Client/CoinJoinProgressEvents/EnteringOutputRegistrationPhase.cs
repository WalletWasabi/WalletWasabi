using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class EnteringOutputRegistrationPhase : RoundStateChanged
{
	public EnteringOutputRegistrationPhase(RoundState roundState, DateTimeOffset timeoutAt) : base(roundState, timeoutAt)
	{
	}
}
