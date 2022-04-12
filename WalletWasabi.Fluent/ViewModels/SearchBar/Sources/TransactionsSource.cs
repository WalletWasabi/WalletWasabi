using System.Linq;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItem;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class TransactionsSource : ISearchItemSource
{
	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Source =>
		TransactionWatcher.Instance.TransactionChanges
			.Transform(ToSearchItem);

	private static ISearchItem ToSearchItem(TransactionEntry r)
	{
		var transactionId = r.HistoryItem.Id.ToString().Trim('0');
		var keywords = r.HistoryItem.Label.Concat(new[] { transactionId });

		var labels = r.HistoryItem.Label is not null ? string.Join(" ", r.HistoryItem.Label) : "";
		return new NonActionableSearchItem(r.HistoryItem.Date.Date.ToShortDateString(), labels, "Transactions", keywords.ToList(), "normal_transaction")
		{
			IsDefault = true,
		};
	}
}