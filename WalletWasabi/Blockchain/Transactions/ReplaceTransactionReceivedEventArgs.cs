using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions
{
	public class ReplaceTransactionReceivedEventArgs : EventArgs
	{
		public ReplaceTransactionReceivedEventArgs(SmartTransaction smartTransaction, IEnumerable<SmartCoin> destroyedCoins, IEnumerable<SmartCoin> restoredCoins)
		{
			SmartTransaction = smartTransaction;
			DestroyedCoins = destroyedCoins;
			RestoredCoins = restoredCoins;
		}

		public SmartTransaction SmartTransaction { get; }
		public IEnumerable<SmartCoin> DestroyedCoins { get; }
		public IEnumerable<SmartCoin> RestoredCoins { get; }
	}
}
