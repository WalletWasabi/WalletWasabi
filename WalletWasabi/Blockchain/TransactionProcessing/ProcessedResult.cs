using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public class ProcessedResult
	{
		private Lazy<bool> _isLikelyOwnCoinJoin;

		public ProcessedResult(SmartTransaction transaction)
		{
			Transaction = Guard.NotNull(nameof(transaction), transaction);
			_isLikelyOwnCoinJoin = new Lazy<bool>(() => Transaction.WalletInputs.Any() && Transaction.Transaction.IsLikelyCoinjoin(), true);
		}

		public SmartTransaction Transaction { get; }

		public bool IsNews =>
			SuccessfullyDoubleSpentCoins.Any()
			|| ReplacedCoins.Any()
			|| RestoredCoins.Any()
			|| NewlyReceivedCoins.Any()
			|| NewlyConfirmedReceivedCoins.Any()
			|| NewlySpentCoins.Any()
			|| NewlyConfirmedSpentCoins.Any()
			|| ReceivedDusts.Any(); // To be fair it isn't necessarily news, the algorithm of the processor can be improved for that. Not sure it is worth it though.

		public bool IsLikelyOwnCoinJoin => _isLikelyOwnCoinJoin.Value;

		/// <summary>
		/// Gets the dust outputs we received in this transaction. We may or may not have known about
		/// them previously. They aren't SmartCoins, because they aren't fully processed.
		/// </summary>
		public List<TxOut> ReceivedDusts { get; } = new List<TxOut>();

		/// <summary>
		/// Gets the coins we received in this transaction.
		/// </summary>
		public List<SmartCoin> ReceivedCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins we received in this transaction and we did not previously know about.
		/// </summary>
		public List<SmartCoin> NewlyReceivedCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins we received in this transaction, we have known already about, but they just got confirmed.
		/// </summary>
		public List<SmartCoin> NewlyConfirmedReceivedCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins we spent in this transaction.
		/// </summary>
		public List<SmartCoin> SpentCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins we spent in this transaction and we did not previously know about.
		/// </summary>
		public List<SmartCoin> NewlySpentCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins we spent in this transaction, we have known already about, but they just got confirmed.
		/// </summary>
		public List<SmartCoin> NewlyConfirmedSpentCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins that we previously had in the mempool, but this confirmed
		/// transaction has successfully invalidated them, because it spends
		/// some of the same inputs.
		/// </summary>
		public List<SmartCoin> SuccessfullyDoubleSpentCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the unconfirmed coins that were replaced by the coins of the transaction.
		/// </summary>
		public List<SmartCoin> ReplacedCoins { get; } = new List<SmartCoin>();

		/// <summary>
		/// Gets the coins that were made unspent again by this double spend transaction.
		/// </summary>
		public List<SmartCoin> RestoredCoins { get; } = new List<SmartCoin>();
	}
}
