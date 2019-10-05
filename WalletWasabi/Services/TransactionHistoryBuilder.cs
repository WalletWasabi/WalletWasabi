using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class TransactionHistoryBuilder
	{
		public WalletService WalletService { get; }

		public TransactionHistoryBuilder(WalletService walletService)
		{
			WalletService = walletService;
		}

		public List<TransactionSummaryData> BuildHistorySummary()
		{
			var walletService = WalletService;

			var txRecordList = new List<TransactionSummaryData>();
			if (walletService is null)
			{
				return txRecordList;
			}

			var processedBlockTimeByHeigh = walletService.ProcessedBlocks?.Values.ToDictionary(x=>x.height, x=>x.dateTime)
				?? new Dictionary<Height, DateTimeOffset>();
			foreach (SmartCoin coin in walletService.Coins)
			{
				var foundTransaction = walletService.TryGetTxFromCache(coin.TransactionId);
				if (foundTransaction is null)
				{
					continue;
				}

				DateTimeOffset dateTime;
				if (foundTransaction.Height.Type == HeightType.Chain)
				{
					if(processedBlockTimeByHeigh.TryGetValue(foundTransaction.Height, out var blockDateTime))
					{
						dateTime = blockDateTime;
					}
					else
					{
						dateTime = DateTimeOffset.UtcNow;
					}
				}
				else
				{
					dateTime = foundTransaction.FirstSeen;
				}

				var found = txRecordList.FirstOrDefault(x => x.TransactionId == coin.TransactionId);
				if (found != null) // if found then update
				{
					var label = found.Label != string.Empty ? found.Label + ", " : "";
					found.DateTime = dateTime; 
					found.Amount += coin.Amount;
					found.Label = $"{label}{coin.Label}";
				}
				else
				{
					txRecordList.Add(new TransactionSummaryData{
						DateTime = dateTime,
						Height = coin.Height, 
						Amount = coin.Amount, 
						Label = coin.Label.ToString(),
						TransactionId = coin.TransactionId});
				}

				if (!coin.Unspent)
				{
					var foundSpenderTransaction = walletService.TryGetTxFromCache(coin.SpenderTransactionId);
					if (foundSpenderTransaction is null)
					{
						throw new InvalidOperationException($"Transaction {coin.SpenderTransactionId} not found.");
					}

					if (foundSpenderTransaction.Height.Type == HeightType.Chain)
					{
						if(processedBlockTimeByHeigh.TryGetValue(foundSpenderTransaction.Height, out var blockDateTime))
						{
							dateTime = blockDateTime;
						}
						else
						{
							dateTime = DateTimeOffset.UtcNow;
						}
					}
					else
					{
						dateTime = foundSpenderTransaction.FirstSeen;
					}

					var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.TransactionId == coin.SpenderTransactionId);
					if (foundSpenderCoin != null) // if found
					{
						foundSpenderCoin.DateTime = dateTime; 
						foundSpenderCoin.Amount -= coin.Amount;
					}
					else
					{
						txRecordList.Add(new TransactionSummaryData{
							DateTime = dateTime,
							Height = foundSpenderTransaction.Height, 
							Amount = (Money.Zero - coin.Amount), 
							Label = "",
							TransactionId = coin.SpenderTransactionId});
					}
				}
			}
			txRecordList = txRecordList.OrderByDescending(x => x.DateTime).ThenBy(x => x.Amount).ToList();
			return txRecordList;
		}
	}
}