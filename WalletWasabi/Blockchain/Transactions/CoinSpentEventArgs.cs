using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions
{
	public class CoinSpentEventArgs : EventArgs
	{
		public SmartCoin SmartCoin { get; private set; }

		public CoinSpentEventArgs(SmartCoin smartCoin)
		{
			SmartCoin = smartCoin;
		}
	}
}
