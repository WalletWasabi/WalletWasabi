using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class SpeedUpHistoryItemViewModel : HistoryItemViewModelBase
{
	public SpeedUpHistoryItemViewModel(UiContext uiContext, TransactionModel transaction, HistoryItemViewModelBase? parent) : base(uiContext, transaction)
	{
		ShowDetailsCommand = parent?.ShowDetailsCommand;
		SpeedUpTransactionCommand = parent?.SpeedUpTransactionCommand;
		CancelTransactionCommand = parent?.CancelTransactionCommand;
	}

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction || Transaction.CanSpeedUpTransaction;
}
