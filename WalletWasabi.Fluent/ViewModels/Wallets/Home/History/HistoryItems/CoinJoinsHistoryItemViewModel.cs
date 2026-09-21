using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinsHistoryItemViewModel(UiContext uiContext, IWalletModel wallet, CoinJoinTransactionGroupModel transaction) : base(uiContext, transaction)
	{
		Transaction = transaction;
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinsDetails(wallet, transaction));
	}

	public override CoinJoinTransactionGroupModel Transaction { get; }
}
