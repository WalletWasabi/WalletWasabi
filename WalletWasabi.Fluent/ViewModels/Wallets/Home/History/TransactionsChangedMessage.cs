using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

public class TransactionsChangedMessage
{
	public HistoryViewModel HistoryViewModel { get; }
	public HistoryItemViewModelBase[] NewHistoryList { get; }

	public TransactionsChangedMessage(HistoryViewModel historyViewModel, HistoryItemViewModelBase[] newHistoryList)
	{
		HistoryViewModel = historyViewModel;
		NewHistoryList = newHistoryList;
	}
}