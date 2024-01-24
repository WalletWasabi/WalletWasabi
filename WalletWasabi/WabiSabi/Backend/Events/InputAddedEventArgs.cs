using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class InputAddedEventArgs : EventArgs
{
	public InputAddedEventArgs(uint256 roundId, Coin coin, bool isCoordinationFeeExempted) : base()
	{
		RoundId = roundId;
		Coin = coin;
		IsCoordinationFeeExempted = isCoordinationFeeExempted;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public bool IsCoordinationFeeExempted { get; }
}
