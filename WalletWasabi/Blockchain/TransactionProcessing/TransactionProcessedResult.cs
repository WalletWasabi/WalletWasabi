using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public class TransactionProcessedResult
	{
		public SmartTransaction Transaction { get; }

		public bool IsWalletRelevant =>
			SuccessfullyDoubleSpentCoins.Any()
			|| ReplacedCoins.Any()
			|| RestoredCoins.Any()
			|| NewlyReceivedCoins.Any()
			|| NewlyConfirmedReceivedCoins.Any()
			|| NewlySpentCoins.Any()
			|| NewlyConfirmedSpentCoins.Any()
			|| ReceivedDusts.Any();

		public bool IsLikelyOwnCoinJoin { get; set; } = false;

		/// <summary>
		/// Dust outputs we received in this transaction. We may or may not have known about
		/// them previously. They aren't SmartCoins, because they aren't fully processed.
		/// </summary>
		public List<TxOut> ReceivedDusts { get; set; } = new List<TxOut>();

		/// <summary>
		/// Coins we received in this transaction.
		/// </summary>
		public List<SmartCoin> ReceivedCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins we received in this transaction and we did not previously known about.
		/// </summary>
		public List<SmartCoin> NewlyReceivedCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins we received in this transaction, we have known already about, but it just got confirmed.
		/// </summary>
		public List<SmartCoin> NewlyConfirmedReceivedCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins we spent in this transaction.
		/// </summary>
		public List<SmartCoin> SpentCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins we spent in this transaction and we did not previously known about.
		/// </summary>
		public List<SmartCoin> NewlySpentCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins we spent in this transaction, we have known already about, but it just got confirmed.
		/// </summary>
		public List<SmartCoin> NewlyConfirmedSpentCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins those we previously had in the mempool, but this confirmed
		/// transaction has successfully invalidated them, because it spends
		/// some of the same inputs.
		/// </summary>
		public List<SmartCoin> SuccessfullyDoubleSpentCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Unconfirmed coins those were replaced by the coins of the transaction.
		/// </summary>
		public List<SmartCoin> ReplacedCoins { get; set; } = new List<SmartCoin>();

		/// <summary>
		/// Coins those were made unspent again by this double spend transaction.
		/// </summary>
		public List<SmartCoin> RestoredCoins { get; set; } = new List<SmartCoin>();

		public TransactionProcessedResult(SmartTransaction transaction)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
		}
	}
}
