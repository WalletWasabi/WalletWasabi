using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class InputAmount : Input
{
	public InputAmount(Money amount, BitcoinAddress address) : base(amount)
	{
		Address = address;
	}

	public BitcoinAddress Address { get; }
}
