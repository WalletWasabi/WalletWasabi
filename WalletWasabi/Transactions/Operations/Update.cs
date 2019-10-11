using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Transactions.Operations
{
	public class Update : ITxStoreOperation
	{
		public IEnumerable<SmartTransaction> Transactions { get; }

		public bool IsEmpty => Transactions is null || !Transactions.Any();

		public Update(params SmartTransaction[] transactions) : this(transactions as IEnumerable<SmartTransaction>)
		{
		}

		public Update(IEnumerable<SmartTransaction> transactions)
		{
			Transactions = transactions;
		}
	}
}
