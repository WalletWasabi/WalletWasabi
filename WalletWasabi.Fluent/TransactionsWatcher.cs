using System.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent;

public class TransactionsWatcher
{
	public IObservable<IChangeSet<TransactionEntry, ComposedKey>> TransactionChanges { get; }
	public static TransactionsWatcher Instance { get; set; } = new();

	public TransactionsWatcher()
	{
		var cache = new SourceCache<TransactionEntry, ComposedKey>(x => x.Key);

		MessageBus.Current.Listen<TransactionsChangedMessage>()
			.Subscribe(msg =>
			{
				cache.Edit(r =>
				{
					var toRemove = r.Items.Where(r => r.Parent == msg.HistoryViewModel).ToList();
					r.Remove(toRemove);
					r.AddOrUpdate(msg.NewHistoryList
						.OrderByDescending(r => r.Date)
						.Select(b => new TransactionEntry(msg.HistoryViewModel, b)));
				});
			});

		TransactionChanges = cache.Connect();
	}
}
