using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Blockchain.Transactions.Operations;

public class Update : ITxStoreOperation
{
	public Update(params SmartTransaction[] transactions) : this(transactions as IEnumerable<SmartTransaction>)
	{
	}

	public Update(IEnumerable<SmartTransaction> transactions)
	{
		Transactions = transactions;
	}

	public IEnumerable<SmartTransaction> Transactions { get; }

	public bool IsEmpty => Transactions is null || !Transactions.Any();
}
