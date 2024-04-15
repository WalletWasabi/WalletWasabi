using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class SpeedUpHistoryItemViewModel : HistoryItemViewModelBase
{
	public SpeedUpHistoryItemViewModel(UiContext uiContext, IWalletModel wallet, TransactionModel transaction, HistoryItemViewModelBase? parent) : base(uiContext, transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(wallet, transaction));
		SpeedUpTransactionCommand = parent?.SpeedUpTransactionCommand;
		CancelTransactionCommand = parent?.CancelTransactionCommand;
	}

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction || Transaction.CanSpeedUpTransaction;
}
