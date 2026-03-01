using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionDependencyNode(Transaction transaction)
{
	public Transaction Transaction { get; } = transaction;
	public List<TransactionDependencyNode> Children { get; } = [];
	public List<TransactionDependencyNode> Parents { get; } = [];
}
