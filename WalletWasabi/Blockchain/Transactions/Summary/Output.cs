using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class Output
{
	public Output(Money amount)
	{
		Amount = amount;
	}

	public Money Amount { get; }
}
