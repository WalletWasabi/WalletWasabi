using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public class DoubleSpendReceivedEventArgs : EventArgs
	{
		public DoubleSpendReceivedEventArgs(SmartTransaction smartTransaction, IEnumerable<SmartCoin> remove) : base()
		{
			SmartTransaction = smartTransaction;
			Remove = remove;
		}

		public SmartTransaction SmartTransaction { get; }
		public IEnumerable<SmartCoin> Remove { get; }
	}
}
