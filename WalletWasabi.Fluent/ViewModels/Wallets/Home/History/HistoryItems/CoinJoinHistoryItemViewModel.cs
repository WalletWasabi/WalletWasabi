using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinHistoryItemViewModel(UiContext uiContext, IWalletModel wallet, CoinJoinTransactionModel transaction) : base(uiContext, transaction)
	{
		Transaction = transaction;
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinDetails(wallet, transaction));
	}

	public override CoinJoinTransactionModel Transaction { get; }
}
