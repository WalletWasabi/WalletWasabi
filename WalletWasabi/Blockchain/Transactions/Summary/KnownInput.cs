using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class KnownInput : Input
{
	public KnownInput(Money amount, BitcoinAddress address) : base(amount)
	{
		Address = address;
	}

	public BitcoinAddress Address { get; }
}
