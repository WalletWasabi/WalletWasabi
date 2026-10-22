using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class RoundSelected(RoundState roundState) : CoinJoinProgressEventArgs
{
	public RoundState RoundState { get; } = roundState;
}
