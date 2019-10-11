using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Transactions.Operations
{
	public interface ITxStoreOperation
	{
		public bool IsEmpty { get; }
	}
}
