using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public abstract class Input
{
	public abstract Money? Amount { get; }
}
