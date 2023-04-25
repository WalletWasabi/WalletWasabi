using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class ForeignInput : Input
{
	public ForeignInput(uint256 transactionId, Money amount) : base(amount)
	{
		TransactionId = transactionId;
	}

	public uint256 TransactionId { get; }
}
