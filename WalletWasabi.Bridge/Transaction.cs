using NBitcoin;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Bridge;

public class Transaction : ITransaction
{
	private readonly TransactionSummary _transactionSummary;

	public Transaction(TransactionSummary transactionSummary)
	{
		_transactionSummary = transactionSummary;
	}

	public Money Amount => _transactionSummary.Amount;

	public uint256 Id => _transactionSummary.TransactionId;

	public ISet<string> Labels => _transactionSummary.Label.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
}
