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
		Dictionary<uint256, TransactionSummary> mapByTxid = new();

		foreach (SmartCoin coin in wallet.GetAllCoins())
		{
			var containingTransaction = coin.Transaction;

			var dateTime = containingTransaction.FirstSeen;
			
			if (mapByTxid.TryGetValue(coin.TransactionId, out TransactionSummary? found)) // If found then update.
			{
				found.FirstSeen = found.FirstSeen < dateTime ? found.FirstSeen : dateTime;
				found.Amount += coin.Amount;
				found.Labels = LabelsArray.Merge(found.Labels, containingTransaction.Labels);
			}
			else
			{
				mapByTxid.Add(coin.TransactionId, new TransactionSummary(containingTransaction, coin.Amount));
			}

			var spenderTransaction = coin.SpenderTransaction;
			if (spenderTransaction is { })
			{
				var spenderTxId = spenderTransaction.GetHash();
				dateTime = spenderTransaction.FirstSeen;
				
				if (mapByTxid.TryGetValue(spenderTxId, out TransactionSummary? foundSpenderCoin)) // If found then update.
				{
					foundSpenderCoin.FirstSeen = foundSpenderCoin.FirstSeen < dateTime ? foundSpenderCoin.FirstSeen : dateTime;
					foundSpenderCoin.Amount -= coin.Amount;
				}
				else
				{
					mapByTxid.Add(spenderTxId, new TransactionSummary(spenderTransaction, Money.Zero - coin.Amount));
				}
			}
		}

		return mapByTxid.Values.OrderByBlockchain().ToList();
	}	
}
