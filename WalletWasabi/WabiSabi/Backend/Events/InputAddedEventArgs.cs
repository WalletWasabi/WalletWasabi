using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class InputAddedEventArgs : EventArgs
{
	public InputAddedEventArgs(uint256 roundId, Coin coin, bool isPayingZeroCoordinationFee) : base()
	{
		RoundId = roundId;
		Coin = coin;
		IsPayingZeroCoordinationFee = isPayingZeroCoordinationFee;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public bool IsPayingZeroCoordinationFee { get; }
}
