using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public class HistoryItemViewModel : ViewModelBase
	{
		private bool _isSelected;

		public HistoryItemViewModel(int orderIndex, TransactionSummary transactionSummary, WalletViewModel walletViewModel, Money balance, IObservable<Unit> updateTrigger)
		{
			TransactionSummary = transactionSummary;
			Date = transactionSummary.DateTime.ToLocalTime();
			IsCoinJoin = transactionSummary.IsLikelyCoinJoinOutput;
			OrderIndex = orderIndex;
			Balance = balance;
			var wallet = walletViewModel.Wallet;

			var confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int)wallet.BitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < Money.Zero)
			{
				OutgoingAmount = amount * -1;
			}
			else
			{
				IncomingAmount = amount;
			}

			Label = transactionSummary.Label.Take(1).ToList();
			FilteredLabel = transactionSummary.Label.Skip(1).ToList();

			ShowDetailsCommand = ReactiveCommand.Create(() => RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(new TransactionDetailsViewModel(transactionSummary, wallet, updateTrigger)));
		}

		public ICommand ShowDetailsCommand { get; }

		public TransactionSummary TransactionSummary { get; }

		public int OrderIndex { get; }

		public Money Balance { get; set; }

		public DateTimeOffset Date { get; set; }

		public bool IsConfirmed { get; }

		public Money? IncomingAmount { get; }

		public Money? OutgoingAmount { get; }

		public List<string> FilteredLabel { get; }

		public List<string> Label { get; }

		public bool IsCoinJoin { get; }

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}
	}
}
