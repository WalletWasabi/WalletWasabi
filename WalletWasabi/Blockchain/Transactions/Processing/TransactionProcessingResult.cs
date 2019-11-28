using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Transactions.Processing
{
	public class TransactionProcessingResult
	{
		public SmartTransaction Transaction { get; set; }
		public IEnumerable<SmartCoin> SpentCoins { get; }
		public IEnumerable<SmartCoin> ReceivedCoins { get; }
		public IEnumerable<SmartCoin> DoubleSpentCoins { get; }
		public IEnumerable<SmartCoin> ReplacedCoins { get; }
		public IEnumerable<SmartCoin> RestoredCoins { get; }

		public TransactionProcessingResult(SmartTransaction transaction, IEnumerable<SmartCoin> spentCoins, IEnumerable<SmartCoin> receivedCoins, IEnumerable<SmartCoin> doubleSpentCoins, IEnumerable<SmartCoin> replacedCoins, IEnumerable<SmartCoin> restoredCoins)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
			SpentCoins = Guard.NotNull(nameof(spentCoins), spentCoins);
			ReceivedCoins = Guard.NotNull(nameof(receivedCoins), receivedCoins);
			DoubleSpentCoins = Guard.NotNull(nameof(doubleSpentCoins), doubleSpentCoins);
			ReplacedCoins = Guard.NotNull(nameof(replacedCoins), replacedCoins);
			RestoredCoins = Guard.NotNull(nameof(restoredCoins), restoredCoins);
		}
	}
}
