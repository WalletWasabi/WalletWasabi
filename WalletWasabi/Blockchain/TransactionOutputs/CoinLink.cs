namespace WalletWasabi.Blockchain.TransactionOutputs
{
	public class CoinLink : ILink
	{
		public CoinLink(SmartCoin sourceCoin, SmartCoin targetCoin, LinkType linkType)
		{
			Coin = sourceCoin;
			TargetCoin = targetCoin;
			LinkType = linkType;
		}

		public SmartCoin Coin { get; }
		public SmartCoin TargetCoin { get; }
		public LinkType LinkType { get; }

	}
}
