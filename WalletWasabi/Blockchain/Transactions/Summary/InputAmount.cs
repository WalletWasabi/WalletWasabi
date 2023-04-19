using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class InputAmount : Input
{
	public InputAmount(Money amount, BitcoinAddress address)
	{
		Amount = amount;
		Address = address;
	}

	public Money Amount { get; }
	public BitcoinAddress Address { get; }
}
