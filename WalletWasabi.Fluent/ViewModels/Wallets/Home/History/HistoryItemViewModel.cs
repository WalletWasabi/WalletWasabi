using System;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public class HistoryItemViewModel
	{
		public HistoryItemViewModel(int orderIndex, TransactionSummary transactionSummary, BitcoinStore bitcoinStore, Money balance)
		{
			Date = transactionSummary.DateTime.ToLocalTime();
			IsCoinJoin = transactionSummary.IsLikelyCoinJoinOutput;
			OrderIndex = orderIndex;
			Balance = balance;
			Labels = transactionSummary.Label;
			TransactionId = transactionSummary.TransactionId.ToString();
			BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
			Confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = Confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < 0)
			{
				OutgoingAmount = (amount * -1).ToString(fplus: false);
			}
			else
			{
				IncomingAmount = amount.ToString(fplus: false);
			}
		}

		public int Confirmations { get; }

		public int BlockHeight { get; }

		public string TransactionId { get; set; }

		public SmartLabel Labels { get; set; }

		public int OrderIndex { get; }

		public Money Balance { get; set; }

		public DateTimeOffset Date { get; set; }

		public bool IsConfirmed { get; }

		public string? IncomingAmount { get; }

		public string? OutgoingAmount { get; }

		public bool IsCoinJoin { get; }
	}
}
