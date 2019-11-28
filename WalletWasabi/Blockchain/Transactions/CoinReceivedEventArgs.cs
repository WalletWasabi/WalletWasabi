using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions
{
	public class CoinReceivedEventArgs : EventArgs
	{
		public SmartCoin SmartCoin { get; private set; }

		public CoinReceivedEventArgs(SmartCoin smartCoin)
		{
			SmartCoin = smartCoin;
		}
	}
}
