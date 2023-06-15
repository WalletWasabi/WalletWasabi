using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class OwnOutput : Output
{
	public bool IsInternal { get; }

	public OwnOutput(Money amount, BitcoinAddress destinationAddress, bool isInternal) : base(amount, destinationAddress)
	{
		IsInternal = isInternal;
	}
}

public abstract class Output
{
	protected Output(Money amount, BitcoinAddress destinationAddress)
	{
		Amount = amount;
		DestinationAddress = destinationAddress;
	}

	public Money Amount { get; }
	public BitcoinAddress DestinationAddress { get; }
}

public class ForeignOutput : Output
{
	public ForeignOutput(Money amount, BitcoinAddress destinationAddress) : base(amount, destinationAddress)
	{
	}
}
