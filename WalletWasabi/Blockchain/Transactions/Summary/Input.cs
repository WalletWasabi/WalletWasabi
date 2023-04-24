using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public abstract class Input
{
	protected Input(Money amount)
	{
		Amount = amount;
	}

	public Money Amount { get; }
}
