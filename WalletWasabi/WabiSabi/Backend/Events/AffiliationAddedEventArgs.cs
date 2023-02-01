using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class AffiliationAddedEventArgs : EventArgs
{
	public AffiliationAddedEventArgs(uint256 roundId, Coin coin, AffiliationFlag affiliationFlag) : base()
	{
		RoundId = roundId;
		Coin = coin;
		AffiliationFlag = affiliationFlag;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public AffiliationFlag AffiliationFlag { get; }
}
