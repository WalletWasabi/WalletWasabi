using System.Globalization;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.Wallets.History
{
	public class HistoryItemViewModel
	{
		public HistoryItemViewModel(TransactionSummary transactionSummary, BitcoinStore bitcoinStore)
		{
			Date = transactionSummary.DateTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

			var confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < 0)
			{
				OutgoingAmount = (amount * -1).ToString(fplus: false);
			}
			else
			{
				IncomingAmount = amount.ToString(fplus: false);
			}

			Labels = transactionSummary.Label;

			IsCoinJoin = transactionSummary.IsLikelyCoinJoinOutput;

			TransactionId = transactionSummary.TransactionId.ToString();
		}

		public string TransactionId { get; }

		public bool IsConfirmed { get; }

		public string Date { get; }

		public SmartLabel Labels { get; }

		public string? IncomingAmount { get; }

		public string? OutgoingAmount { get; }

		public bool IsCoinJoin { get; }
	}
}
