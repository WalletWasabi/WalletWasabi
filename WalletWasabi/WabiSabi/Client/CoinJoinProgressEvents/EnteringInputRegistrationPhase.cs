using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class EnteringInputRegistrationPhase : RoundStateChanged
{
	public EnteringInputRegistrationPhase(RoundState roundState, DateTimeOffset timeoutAt) : base(roundState, timeoutAt)
	{
	}
}
