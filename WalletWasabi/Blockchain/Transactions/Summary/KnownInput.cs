using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class KnownInput : Input
{
	public KnownInput(Money amount, BitcoinAddress address)
	{
		Address = address;
		Amount = amount;
	}

	public override Money? Amount { get; }

	public BitcoinAddress Address { get; }
}
