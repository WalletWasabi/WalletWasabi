using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions
{
	public class DoubleSpendReceivedEventArgs : EventArgs
	{
		public DoubleSpendReceivedEventArgs(SmartTransaction smartTransaction, IEnumerable<SmartCoin> remove)
		{
			SmartTransaction = smartTransaction;
			Remove = remove;
		}

		public SmartTransaction SmartTransaction { get; }
		public IEnumerable<SmartCoin> Remove { get; }
	}
}
