using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public class Output
{
	public Money Amount { get; }
	public BitcoinAddress Destination { get; }
	public bool IsSpent { get; }

	public Output(Money amount, BitcoinAddress destination, bool isSpent)
	{
		Amount = amount;
		Destination = destination;
		IsSpent = isSpent;
	}
}
