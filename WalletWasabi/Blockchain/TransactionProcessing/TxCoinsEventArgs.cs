using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public class TxCoinsEventArgs : EventArgs
	{
		public TxCoinsEventArgs(SmartTransaction smartTransaction, IEnumerable<SmartCoin> coins) : base()
		{
			SmartTransaction = smartTransaction;
			Coins = coins;
		}

		public SmartTransaction SmartTransaction { get; }
		public IEnumerable<SmartCoin> Coins { get; }
	}
}
