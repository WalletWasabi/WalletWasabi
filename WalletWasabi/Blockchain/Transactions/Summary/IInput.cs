using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public interface IInput
{
	Money? Amount { get; }
	bool? Confirmed { get; }
}
