using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class ForeignInput : IInput
{
	public ForeignInput(uint256 transactionId)
	{
		TransactionId = transactionId;
	}

	public uint256 TransactionId { get; }
	public virtual Money? Amount => null;
}
