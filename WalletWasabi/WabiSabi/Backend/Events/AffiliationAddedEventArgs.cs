using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class AffiliationAddedEventArgs : EventArgs
{
	public AffiliationAddedEventArgs(uint256 roundId, Coin coin, string affiliationId) : base()
	{
		RoundId = roundId;
		Coin = coin;
		AffiliationId = affiliationId;
	}

	public uint256 RoundId { get; }
	public Coin Coin { get; }
	public string AffiliationId { get; }
}
