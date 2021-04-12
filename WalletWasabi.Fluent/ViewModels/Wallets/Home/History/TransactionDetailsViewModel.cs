using System;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "Transaction Details")]
	public partial class TransactionDetailsViewModel : RoutableViewModel
	{
		public TransactionDetailsViewModel(TransactionSummary transactionSummary, BitcoinStore bitcoinStore)
		{
			Date = transactionSummary.DateTime.ToLocalTime();
			TransactionId = transactionSummary.TransactionId.ToString();
			Labels = transactionSummary.Label;
			BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
			Confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = Confirmations > 0;
			Amount = transactionSummary.Amount;

			NextCommand = ReactiveCommand.Create(OnNext);
		}

		public Money Amount { get; set; }

		public bool IsConfirmed { get; set; }

		public int Confirmations { get; set; }

		public int BlockHeight { get; set; }

		public SmartLabel Labels { get; set; }

		public string TransactionId { get; set; }

		public DateTimeOffset Date { get; set; }

		private void OnNext()
		{
			Navigate().Clear();
		}
	}
}
