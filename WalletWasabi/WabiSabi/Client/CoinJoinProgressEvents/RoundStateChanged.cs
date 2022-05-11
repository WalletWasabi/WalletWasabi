using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class RoundStateChanged : CoinJoinProgressEventArgs
{
	public RoundStateChanged(RoundState roundState, DateTimeOffset timeoutAt)
	{
		RoundState = roundState;
		TimeoutAt = timeoutAt;
	}

	public RoundState RoundState { get; }
	public DateTimeOffset TimeoutAt { get; }
}
