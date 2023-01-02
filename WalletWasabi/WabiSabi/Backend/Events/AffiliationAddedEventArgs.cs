using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class AffiliationAddedEventArgs : EventArgs
{
	public AffiliationAddedEventArgs(uint256 roundId, Coin coin, AffiliationFlag affiliationFlag, bool isPayingZeroCoordinationFee) : base()
	{
		RoundId = roundId;
		Coin = coin;
		AffiliationFlag = affiliationFlag;
		IsPayingZeroCoordinationFee = isPayingZeroCoordinationFee;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public AffiliationFlag AffiliationFlag { get; }
	public bool IsPayingZeroCoordinationFee { get; }
}
