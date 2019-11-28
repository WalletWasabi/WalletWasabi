using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions
{
	public class SpenderConfirmedEventArgs : EventArgs
	{
		public SmartCoin SmartCoin { get; private set; }

		public SpenderConfirmedEventArgs(SmartCoin smartCoin)
		{
			SmartCoin = smartCoin;
		}
	}
}
