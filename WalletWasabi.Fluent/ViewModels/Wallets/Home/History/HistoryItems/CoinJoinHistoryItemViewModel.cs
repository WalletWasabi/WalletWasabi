using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinHistoryItemViewModel(
		int orderIndex,
		TransactionSummary transactionSummary,
		WalletViewModel walletVm,
		Money balance,
		bool isSingleCoinJoinTransaction)
		: base(orderIndex, transactionSummary)
	{
		IsConfirmed = transactionSummary.IsConfirmed();
		Date = transactionSummary.DateTime.ToLocalTime();
		Balance = balance;
		IsCoinJoin = true;
		CoinJoinTransaction = transactionSummary;
		IsSingleCoinJoinTransaction = isSingleCoinJoinTransaction;

		var confirmations = transactionSummary.GetConfirmations();
		ConfirmedToolTip = $"{confirmations} confirmation{TextHelpers.AddSIfPlural(confirmations)}";

		var amount = transactionSummary.Amount;
		SetAmount(amount);

		ShowDetailsCommand = ReactiveCommand.Create(() =>
			RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(
				new CoinJoinDetailsViewModel(this, walletVm.UiTriggers.TransactionsUpdateTrigger)));

		DateString = $"{Date.ToLocalTime():MM/dd/yyyy HH:mm}";
	}

	public bool IsSingleCoinJoinTransaction { get; }

	public TransactionSummary CoinJoinTransaction { get; private set; }
}
