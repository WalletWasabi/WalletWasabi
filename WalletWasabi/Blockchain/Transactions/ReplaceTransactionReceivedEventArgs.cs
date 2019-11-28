using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions
{
	public class ReplaceTransactionReceivedEventArgs
	{
		public ReplaceTransactionReceivedEventArgs(SmartTransaction smartTransaction, IEnumerable<SmartCoin> replacedCoins, IEnumerable<SmartCoin> restoredCoins)
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
