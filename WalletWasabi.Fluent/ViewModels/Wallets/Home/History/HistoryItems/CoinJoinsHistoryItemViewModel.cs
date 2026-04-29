using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinsHistoryItemViewModel(UiContext uiContext, IWalletModel wallet, TransactionModel transaction) : base(uiContext, transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinsDetails(wallet, transaction));
	}
}
