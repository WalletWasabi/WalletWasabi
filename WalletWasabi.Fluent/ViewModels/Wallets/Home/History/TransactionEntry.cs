using WalletWasabi.Fluent.ViewModels.SearchBar;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

public class TransactionEntry
{
	public HistoryItemViewModelBase HistoryItem { get; }

	public TransactionEntry(HistoryViewModel parent, HistoryItemViewModelBase historyItem)
	{
		Parent = parent;
		HistoryItem = historyItem;
		Key = new ComposedKey(Parent, HistoryItem.Id);
	}

	public ComposedKey Key { get; }
	public HistoryViewModel Parent { get; set; }
}