using System;
using System.Reactive;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems
{
	public class TransactionHistoryItemViewModel : HistoryItemViewModelBase
	{
		public TransactionHistoryItemViewModel(int orderIndex, TransactionSummary transactionSummary, WalletViewModel walletViewModel, Money balance, IObservable<Unit> updateTrigger)
			: base(orderIndex, transactionSummary, balance)
		{
			var amount = transactionSummary.Amount;
			if (amount < Money.Zero)
			{
				OutgoingAmount = amount * -1;
			}
			else
			{
				IncomingAmount = amount;
			}

			ShowDetailsCommand = ReactiveCommand.Create(() =>
				RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(
					new TransactionDetailsViewModel(transactionSummary, walletViewModel.Wallet, updateTrigger)));

			UpdateDateString();
		}

		protected sealed override void UpdateDateString()
		{
			DateString = $"{Date.ToLocalTime():MM/dd/yyyy HH:mm}";
		}
	}
}
