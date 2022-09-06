using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class UnknownInput : Input
{
	public UnknownInput(uint256 transactionId)
	{
		TransactionId = transactionId;
	}

	public uint256 TransactionId { get; }
}
