using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

public class TransactionEntry
{
	public TransactionEntry(
		HistoryViewModel parent,
		HistoryItemViewModelBase historyItem,
		WalletViewModel wallet)
	{
		Parent = parent;
		HistoryItem = historyItem;
		Wallet = wallet;
		Key = new ComposedKey(Parent, HistoryItem.Id);
	}

	public HistoryItemViewModelBase HistoryItem { get; }
	public WalletViewModel Wallet { get; }

	public ComposedKey Key { get; }
	public HistoryViewModel Parent { get; }
}
