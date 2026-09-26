using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class SpeedUpHistoryItemViewModel : HistoryItemViewModelBase
{
	public SpeedUpHistoryItemViewModel(UiContext uiContext, IWalletModel wallet, RegularTransactionModel transaction, HistoryItemViewModelBase? parent) : base(uiContext, transaction)
	{
		Transaction = transaction;
		CanBeCancelled = transaction.CanCancelTransaction;
		HasBeenSpedUp = transaction.HasBeenSpedUp;
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(wallet, transaction));
		CancelTransactionCommand = parent?.CancelTransactionCommand;
	}

	public override RegularTransactionModel Transaction { get; }

	public bool TransactionOperationsVisible => CanBeCancelled;
}
