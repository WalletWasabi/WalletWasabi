using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class AffiliationAddedEventArgs : EventArgs
{
	public AffiliationAddedEventArgs(uint256 roundId, Coin coin, string affiliationFlag) : base()
	{
		RoundId = roundId;
		Coin = coin;
		AffiliationFlag = affiliationFlag;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public string AffiliationFlag { get; }
}
