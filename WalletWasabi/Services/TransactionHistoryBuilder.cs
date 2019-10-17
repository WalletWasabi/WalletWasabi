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

		public List<TransactionSummary> BuildHistorySummary()
		{
			var walletService = WalletService;

			var txRecordList = new List<TransactionSummary>();
			if (walletService is null)
			{
				return txRecordList;
			}

			var processedBlockTimeByHeight = walletService.ProcessedBlocks?.Values.ToDictionary(x => x.height, x => x.dateTime)
				?? new Dictionary<Height, DateTimeOffset>();
			var allCoins = ((CoinsRegistry)walletService.Coins).AsAllCoinsView();
			foreach (SmartCoin coin in allCoins)
			{
				var txId = coin.TransactionId;
				if (txId is null || !walletService.BitcoinStore.TransactionStore.TryGetTransaction(txId, out SmartTransaction foundTransaction))
				{
					continue;
				}

				DateTimeOffset dateTime;
				if (foundTransaction.Height.Type == HeightType.Chain)
				{
					if (processedBlockTimeByHeight.TryGetValue(foundTransaction.Height, out var blockDateTime))
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
					var label = !string.IsNullOrEmpty(found.Label) ? found.Label + ", " : "";
					found.DateTime = dateTime;
					found.Amount += coin.Amount;
					found.Label = $"{label}{coin.Label}";
				}
				else
				{
					txRecordList.Add(new TransactionSummary
					{
						DateTime = dateTime,
						Height = coin.Height,
						Amount = coin.Amount,
						Label = coin.Label.ToString(),
						TransactionId = coin.TransactionId,
						BlockIndex = foundTransaction.BlockIndex
					});
				}

				if (!coin.Unspent)
				{
					if (!walletService.BitcoinStore.TransactionStore.TryGetTransaction(coin.SpenderTransactionId, out SmartTransaction foundSpenderTransaction))
					{
						throw new InvalidOperationException($"Transaction {coin.SpenderTransactionId} not found.");
					}

					if (foundSpenderTransaction.Height.Type == HeightType.Chain)
					{
						if (processedBlockTimeByHeight.TryGetValue(foundSpenderTransaction.Height, out var blockDateTime))
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
						txRecordList.Add(new TransactionSummary
						{
							DateTime = dateTime,
							Height = foundSpenderTransaction.Height,
							Amount = (Money.Zero - coin.Amount),
							Label = "",
							TransactionId = coin.SpenderTransactionId,
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
