using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class TransactionsSource : ISearchItemSource
{
	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes =>
		TransactionsWatcher.Instance.TransactionChanges
			.Transform(ToSearchItem);

	private static ISearchItem ToSearchItem(TransactionEntry r)
	{
		var transactionId = new string(r.HistoryItem.Id.ToString().Trim('0').Take(10).ToArray());
		return new ActionableItem(transactionId, $"Found in {r.Wallet.WalletName}", async () => r.Parent.SelectTransaction(r.HistoryItem.Id), "Transactions");
	}
}
