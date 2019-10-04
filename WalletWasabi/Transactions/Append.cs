using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Transactions
{
	public class Append : ITxStoreOperation
	{
		public IEnumerable<SmartTransaction> Transactions { get; }

		public Append(IEnumerable<SmartTransaction> transactions)
		{
			Transactions = transactions;
		}
	}
}
