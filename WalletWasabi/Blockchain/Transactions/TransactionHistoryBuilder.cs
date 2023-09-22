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
		var txRecordList = new List<TransactionSummary>();

		foreach (SmartCoin coin in wallet.GetAllCoins())
		{
			var containingTransaction = coin.Transaction;

			var dateTime = containingTransaction.FirstSeen;
			var found = txRecordList.FirstOrDefault(x => x.GetHash() == coin.TransactionId);
			if (found is { }) // if found then update
			{
				found.FirstSeen = found.FirstSeen < dateTime ? found.FirstSeen : dateTime;
				found.Amount += coin.Amount;
				found.Labels = LabelsArray.Merge(found.Labels, containingTransaction.Labels);
			}
			else
			{
				txRecordList.Add(new TransactionSummary(containingTransaction, coin.Amount));
			}

			var spenderTransaction = coin.SpenderTransaction;
			if (spenderTransaction is { })
			{
				var spenderTxId = spenderTransaction.GetHash();
				dateTime = spenderTransaction.FirstSeen;
				var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.GetHash() == spenderTxId);
				if (foundSpenderCoin is { }) // if found
				{
					foundSpenderCoin.FirstSeen = foundSpenderCoin.FirstSeen < dateTime ? foundSpenderCoin.FirstSeen : dateTime;
					foundSpenderCoin.Amount -= coin.Amount;
				}
				else
				{
					txRecordList.Add(new TransactionSummary(spenderTransaction, Money.Zero - coin.Amount));
				}
			}
		}
		txRecordList = txRecordList.OrderByBlockchain().ToList();
		return txRecordList;
	}	
}
