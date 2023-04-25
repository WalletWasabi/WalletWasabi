using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class Output
{
	public Output(Money amount, BitcoinAddress destination, bool isSpent)
	{
		Amount = amount;
		Destination = destination;
		IsSpent = isSpent;
	}

	public Money Amount { get; }
	public BitcoinAddress Destination { get; }
	public bool IsSpent { get; }
}
