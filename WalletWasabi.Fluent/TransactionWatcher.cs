using System.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent;

public class TransactionWatcher
{
	public static TransactionWatcher Instance { get; set; } = new();

	public SourceCache<TransactionEntry, ComposedKey> TransactionCache { get; }

	public TransactionWatcher()
	{
		TransactionCache = new SourceCache<TransactionEntry, ComposedKey>(x => x.Key);
		MessageBus.Current.Listen<TransactionsChangedMessage>()
			.Subscribe(msg =>
			{
				TransactionCache.Edit(r =>
				{
					var toRemove = r.Items.Where(r => r.Parent == msg.HistoryViewModel).ToList();
					r.Remove(toRemove);
					r.AddOrUpdate(msg.NewHistoryList.Select(b => new TransactionEntry(msg.HistoryViewModel, b)));
				});
			});
	}
}