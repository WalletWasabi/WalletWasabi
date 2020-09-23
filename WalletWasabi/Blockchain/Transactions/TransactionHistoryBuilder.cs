using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Models;
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
				var txId = coin.TransactionId;
				if (txId is null || !wallet.BitcoinStore.TransactionStore.TryGetTransaction(txId, out SmartTransaction foundTransaction))
				{
					continue;
				}

				DateTimeOffset dateTime;
				if (foundTransaction.Height.Type == HeightType.Chain)
				{
					if (wallet.BitcoinStore.SmartHeaderChain.TryGetHeader((uint)foundTransaction.Height.Value, out SmartHeader header))
					{
						dateTime = header.BlockTime;
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
					found.DateTime = dateTime;
					found.Amount += coin.Amount;
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
						BlockIndex = foundTransaction.BlockIndex,
						IsLikelyCoinJoinOutput = coin.IsLikelyCoinJoinOutput is true
					});
				}

				if (!coin.Unspent)
				{
					if (!wallet.BitcoinStore.TransactionStore.TryGetTransaction(coin.SpenderTransactionId, out SmartTransaction foundSpenderTransaction))
					{
						throw new InvalidOperationException($"Transaction {coin.SpenderTransactionId} not found.");
					}

					if (foundSpenderTransaction.Height.Type == HeightType.Chain)
					{
						if (wallet.BitcoinStore.SmartHeaderChain.TryGetHeader((uint)foundSpenderTransaction.Height.Value, out SmartHeader header))
						{
							dateTime = header.BlockTime;
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
							Amount = Money.Zero - coin.Amount,
							Label = SmartLabel.Empty,
							TransactionId = coin.SpenderTransactionId,
							BlockIndex = foundSpenderTransaction.BlockIndex,
							IsLikelyCoinJoinOutput = coin.IsLikelyCoinJoinOutput is true
						});
					}
				}
			}
			txRecordList = txRecordList.OrderByBlockchain().ToList();
			return txRecordList;
		}
	}
}
