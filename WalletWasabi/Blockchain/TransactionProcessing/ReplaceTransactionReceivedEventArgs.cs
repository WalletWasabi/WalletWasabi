using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public class ReplaceTransactionReceivedEventArgs : EventArgs
	{
		public ReplaceTransactionReceivedEventArgs(SmartTransaction smartTransaction, IEnumerable<SmartCoin> replacedCoins, IEnumerable<SmartCoin> restoredCoins) : base()
		{
			SmartTransaction = smartTransaction;
			ReplacedCoins = replacedCoins;
			RestoredCoins = restoredCoins;
		}

		public SmartTransaction SmartTransaction { get; }
		public IEnumerable<SmartCoin> ReplacedCoins { get; }
		public IEnumerable<SmartCoin> RestoredCoins { get; }
	}
}
