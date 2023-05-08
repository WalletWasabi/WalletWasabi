using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class Output
{
	public Output(Money amount, BitcoinAddress destination)
	{
		Amount = amount;
		Destination = destination;
	}

	public Money Amount { get; }
	public BitcoinAddress Destination { get; }
}
