using System.Globalization;
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
			Date = transactionSummary.DateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

			var confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			Confirmed = confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < 0)
			{
				OutgoingAmount = amount.ToString(fplus: true, trimExcessZero: true);
			}
			else
			{
				IncomingAmount = amount.ToString(fplus: true, trimExcessZero: true);
			}

			Labels = transactionSummary.Label;
		}

		public bool Confirmed { get; }

		public string Date { get; }

		public SmartLabel Labels { get; }

		public string? IncomingAmount { get; }

		public string? OutgoingAmount { get; }
	}
}
