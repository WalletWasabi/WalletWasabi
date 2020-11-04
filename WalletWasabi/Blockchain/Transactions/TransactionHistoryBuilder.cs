using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;

namespace WalletWasabi.Blockchain.Transactions
{
	public class TransactionHistoryBuilder
	{
		public TransactionHistoryBuilder(Wallet wallet)
		{
			Wallet = wallet;
		}

		public Wallet Wallet { get; }

		public List<TransactionSummary> BuildHistorySummary()
		{
			var wallet = Wallet;

			var txRecordList = new List<TransactionSummary>();
			if (wallet is null)
			{
				return txRecordList;
			}

			var allCoins = ((CoinsRegistry)wallet.Coins).AsAllCoinsView();
			foreach (SmartCoin coin in allCoins)
			{
				var foundTransaction = coin.Transaction;

				var dateTime = foundTransaction.FirstSeen;
				var found = txRecordList.FirstOrDefault(x => x.TransactionId == coin.TransactionId);
				if (found is { }) // if found then update
				{
					found.DateTime = found.DateTime < dateTime ? found.DateTime : dateTime;
					found.Amount += coin.Amount;
					found.Label = SmartLabel.Merge(found.Label, foundTransaction.Label);
				}
				else
				{
					txRecordList.Add(new TransactionSummary
					{
						DateTime = dateTime,
						Height = coin.Height,
						Amount = coin.Amount,
						Label = foundTransaction.Label,
						TransactionId = coin.TransactionId,
						BlockIndex = foundTransaction.BlockIndex
					});
				}

				var foundSpenderTransaction = coin.SpenderTransaction;
				if (foundSpenderTransaction is { })
				{
					var spenderTxId = foundSpenderTransaction.GetHash();
					dateTime = foundSpenderTransaction.FirstSeen;
					var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.TransactionId == spenderTxId);
					if (foundSpenderCoin is { }) // if found
					{
						foundSpenderCoin.DateTime = foundSpenderCoin.DateTime < dateTime ? foundSpenderCoin.DateTime : dateTime;
						foundSpenderCoin.Amount -= coin.Amount;
					}
					else
					{
						txRecordList.Add(new TransactionSummary
						{
							DateTime = dateTime,
							Height = foundSpenderTransaction.Height,
							Amount = Money.Zero - coin.Amount,
							Label = foundSpenderTransaction.Label,
							TransactionId = spenderTxId,
							BlockIndex = foundSpenderTransaction.BlockIndex
						});
					}
				}
			}
			txRecordList = txRecordList.OrderByBlockchain().ToList();
			return txRecordList;
		}
	}
}
