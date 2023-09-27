using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionHistoryBuilder
{
	public static List<TransactionSummary> BuildHistorySummary(Wallet wallet)
	{
		Dictionary<uint256, TransactionSummary> txRecordList = new();

		foreach (SmartCoin coin in wallet.GetAllCoins())
		{
			var containingTransaction = coin.Transaction;

			var dateTime = containingTransaction.FirstSeen;
			
			if (txRecordList.TryGetValue(coin.TransactionId, out TransactionSummary? found)) // If found then update.
			{
				found.FirstSeen = found.FirstSeen < dateTime ? found.FirstSeen : dateTime;
				found.Amount += coin.Amount;
				found.Labels = LabelsArray.Merge(found.Labels, containingTransaction.Labels);
			}
			else
			{
				txRecordList.Add(coin.TransactionId, new TransactionSummary(containingTransaction, coin.Amount));
			}

			var spenderTransaction = coin.SpenderTransaction;
			if (spenderTransaction is { })
			{
				var spenderTxId = spenderTransaction.GetHash();
				dateTime = spenderTransaction.FirstSeen;
				
				if (txRecordList.TryGetValue(spenderTxId, out TransactionSummary? foundSpenderCoin)) // If found then update.
				{
					foundSpenderCoin.FirstSeen = foundSpenderCoin.FirstSeen < dateTime ? foundSpenderCoin.FirstSeen : dateTime;
					foundSpenderCoin.Amount -= coin.Amount;
				}
				else
				{
					txRecordList.Add(spenderTxId, new TransactionSummary(spenderTransaction, Money.Zero - coin.Amount));
				}
			}
		}

		return txRecordList.Values.OrderByBlockchain().ToList();
	}	
}
