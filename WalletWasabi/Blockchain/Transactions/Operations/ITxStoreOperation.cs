using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Blockchain.Transactions.Operations
{
	public interface ITxStoreOperation
	{
		public bool IsEmpty { get; }
	}
}
