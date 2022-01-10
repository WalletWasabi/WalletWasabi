using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class TransactionInfo : PartialTransactionInfo
{
	public TransactionInfo(BitcoinAddress address)
	{
		Address = address;
	}

	public BitcoinAddress Address { get; }
}