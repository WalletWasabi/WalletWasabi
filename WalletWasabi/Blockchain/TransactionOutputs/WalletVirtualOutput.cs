namespace WalletWasabi.Blockchain.TransactionOutputs;

public class WalletVirtualOutput
{
	public WalletVirtualOutput(ISet<SmartCoin> coins)
	{
		Coins = coins;
		Amount = coins.Sum(x => x.Amount);
	}

	public Money Amount { get; }
	public ISet<SmartCoin> Coins { get; }
}
