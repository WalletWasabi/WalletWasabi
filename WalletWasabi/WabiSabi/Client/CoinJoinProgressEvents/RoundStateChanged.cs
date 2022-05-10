using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class RoundStateChanged : CoinJoinProgressEventArgs
{
	public RoundStateChanged(RoundState roundState, DateTimeOffset timeout)
	{
		RoundState = roundState;
		Timeout = timeout;
	}

	public RoundState RoundState { get; }
	public DateTimeOffset Timeout { get; }
}
