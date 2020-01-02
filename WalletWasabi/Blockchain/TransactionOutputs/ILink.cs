namespace WalletWasabi.Blockchain.TransactionOutputs
{
	public interface ILink
	{
		SmartCoin Coin { get; }
		LinkType LinkType { get; }
	}
}
