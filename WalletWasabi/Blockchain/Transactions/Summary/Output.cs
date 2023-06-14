using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class Output
{
	public Output(Money amount, BitcoinAddress destinationAddress)
	{
		Amount = amount;
		DestinationAddress = destinationAddress;
	}

	public Money Amount { get; }
	public BitcoinAddress DestinationAddress { get; }
}
