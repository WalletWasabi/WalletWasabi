using System;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public class HistoryItemViewModel
	{
		public HistoryItemViewModel(int orderIndex, TransactionSummary transactionSummary, BitcoinStore bitcoinStore, Money balance)
		{
			TransactionSummary = transactionSummary;
			Date = transactionSummary.DateTime.ToLocalTime();
			IsCoinJoin = transactionSummary.IsLikelyCoinJoinOutput;
			OrderIndex = orderIndex;
			Balance = balance;

			var confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < 0)
			{
				Amount = amount * -1;
				Type = TransactionType.Outgoing;
			}
			else
			{
				Amount = amount;
				Type = TransactionType.Incoming;
			}

			if (IsCoinJoin)
			{
				Type = TransactionType.CoinJoin;
			}

			//TODO: Self Spend type
		}

		public TransactionType Type { get; }

		public TransactionSummary TransactionSummary { get; }

		public int OrderIndex { get; }

		public Money Balance { get; set; }

		public DateTimeOffset Date { get; set; }

		public bool IsConfirmed { get; }

		public Money Amount { get; }

		public bool IsCoinJoin { get; }
	}
}
