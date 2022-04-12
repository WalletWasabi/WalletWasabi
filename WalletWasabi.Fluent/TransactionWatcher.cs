using System.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent;

public class TransactionWatcher
{
	public IObservable<IChangeSet<TransactionEntry, ComposedKey>> TransactionChanges { get; }
	public static TransactionWatcher Instance { get; set; } = new();

	public TransactionWatcher()
	{
		var cache = new SourceCache<TransactionEntry, ComposedKey>(x => x.Key);

		MessageBus.Current.Listen<TransactionsChangedMessage>()
			.Subscribe(msg =>
			{
				cache.Edit(r =>
				{
					var toRemove = r.Items.Where(r => r.Parent == msg.HistoryViewModel).ToList();
					r.Remove(toRemove);
					r.AddOrUpdate(msg.NewHistoryList.Select(b => new TransactionEntry(msg.HistoryViewModel, b)));
				});
			});

		TransactionChanges = cache.Connect();
	}
}