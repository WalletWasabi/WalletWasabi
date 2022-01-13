using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Blockchain.Transactions.Operations;

public class Append : ITxStoreOperation
{
	public Append(params SmartTransaction[] transactions) : this(transactions as IEnumerable<SmartTransaction>)
	{
	}

	public Append(IEnumerable<SmartTransaction> transactions)
	{
		Transactions = transactions;
	}

	public IEnumerable<SmartTransaction> Transactions { get; }

	public bool IsEmpty => Transactions is null || !Transactions.Any();
}
