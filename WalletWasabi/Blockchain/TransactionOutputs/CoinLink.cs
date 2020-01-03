namespace WalletWasabi.Blockchain.TransactionOutputs
{
	public class CoinLink : ILink
	{
		public SmartCoin Coin { get; }
		public SmartCoin TargetCoin { get; }
		public LinkType LinkType { get; }

		public CoinLink(SmartCoin sourceCoin, SmartCoin targetCoin, LinkType linkType)
		{
			Coin = sourceCoin;
			TargetCoin = targetCoin;
			LinkType = linkType;
		}
	}
}
