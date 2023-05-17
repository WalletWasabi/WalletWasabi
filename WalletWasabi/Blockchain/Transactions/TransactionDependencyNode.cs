using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionDependencyNode
{
	public List<TransactionDependencyNode> Children { get; } = new List<TransactionDependencyNode>();
	public List<TransactionDependencyNode> Parents { get; } = new List<TransactionDependencyNode>();
	public Transaction Transaction { get; set; }
}
