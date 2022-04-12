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
		var keywords = r.HistoryItem.Label?.Concat(new[] { transactionId }) ?? Enumerable.Empty<string>();
		return new NonActionableSearchItem(new TransactionSearchItem(r.HistoryItem), "", "Transactions", keywords.ToList(), "normal_transaction");
	}
}