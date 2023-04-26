using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class ForeignInput : Input
{
	public ForeignInput(uint256 transactionId)
	{
		TransactionId = transactionId;
	}

	public uint256 TransactionId { get; }
	public override Money? Amount => null;
}
