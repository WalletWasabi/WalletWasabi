using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent;

public class TransactionsChangedMessage
{
	public TransactionsChangedMessage(
		HistoryViewModel historyViewModel,
		HistoryItemViewModelBase[] newHistoryList,
		WalletViewModel walletViewModel)
	{
		HistoryViewModel = historyViewModel;
		NewHistoryList = newHistoryList;
		WalletViewModel = walletViewModel;
	}

	public HistoryViewModel HistoryViewModel { get; }
	public HistoryItemViewModelBase[] NewHistoryList { get; }
	public WalletViewModel WalletViewModel { get; }
}
