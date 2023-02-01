using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class InputAddedEventArgs : EventArgs
{
	public InputAddedEventArgs(uint256 roundId, Coin coin, bool isCoordinationFeeExempted) : base()
	{
		RoundId = roundId;
		Coin = coin;
		IsFeeExempted = isCoordinationFeeExempted;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public bool IsFeeExempted { get; }
}
