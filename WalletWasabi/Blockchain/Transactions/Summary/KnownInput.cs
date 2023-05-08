using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class KnownInput : IInput
{
	public KnownInput(Money amount, BitcoinAddress address)
	{
		Address = address;
		Amount = amount;
	}

	public virtual Money? Amount { get; }

	public BitcoinAddress Address { get; }
}
