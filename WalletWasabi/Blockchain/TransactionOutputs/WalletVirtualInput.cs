namespace WalletWasabi.Blockchain.TransactionOutputs;

public class WalletVirtualInput
{
	public WalletVirtualInput(ISet<SmartCoin> coins)
	{
		Coins = coins;
		HdPubKey = coins.Select(x => x.HdPubKey).Distinct().Single();
		Amount = coins.Sum(x => x.Amount);
	}

	public ISet<SmartCoin> Coins { get; }
	public HdPubKey HdPubKey { get; }
	public Money Amount { get; }
}
