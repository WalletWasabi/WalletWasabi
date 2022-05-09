using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Events;

public class RoundStateChangedEventArgs : StatusChangedEventArgs
{
	public RoundStateChangedEventArgs(Wallet wallet, RoundState roundState, DateTimeOffset phaseEndTime)
		: base(wallet)
	{
		RoundState = roundState;
		PhaseEndTime = phaseEndTime;
	}

	public RoundState RoundState { get; }

	public DateTimeOffset PhaseEndTime { get; }
}
