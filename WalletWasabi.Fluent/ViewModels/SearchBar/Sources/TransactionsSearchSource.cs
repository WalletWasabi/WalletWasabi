using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class TransactionsSearchSource : ReactiveObject, ISearchSource, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public TransactionsSearchSource(IObservable<string> query)
	{
		var sourceCache = new SourceCache<ISearchItem, ComposedKey>(x => x.Key)
			.DisposeWith(_disposables);

		query
			.Select(s => s.Length > 5 ? PerformSearch(s) : Enumerable.Empty<ISearchItem>())
			.Do(results => sourceCache.Edit(e => e.Load(results)))
			.Subscribe()
			.DisposeWith(_disposables);

		Changes = sourceCache.Connect();
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }

	private static bool Contains(string queryStr, HistoryItemViewModelBase historyItemViewModelBase)
	{
		return historyItemViewModelBase.Id.ToString().Contains(queryStr, StringComparison.CurrentCultureIgnoreCase);
	}

	private static Task NavigateTo(WalletViewModel wallet, HistoryItemViewModelBase item)
	{
		wallet.NavigateAndHighlight(item.Id);
		return Task.CompletedTask;
	}

	private static string GetIcon(HistoryItemViewModelBase historyItemViewModelBase)
	{
		return historyItemViewModelBase switch
		{
			CoinJoinHistoryItemViewModel => "shield_regular",
			CoinJoinsHistoryItemViewModel => "shield_regular",
			TransactionHistoryItemViewModel => "normal_transaction",
			_ => ""
		};
	}

	private static IEnumerable<(WalletViewModel, HistoryItemViewModelBase)> Flatten(
		IEnumerable<(WalletViewModel Wallet, IEnumerable<HistoryItemViewModelBase> Transactions)>
			walletTransactions)
	{
		return walletTransactions.SelectMany(t => t.Transactions.Select(tran => (t.Wallet, tran)));
	}

	private static ISearchItem ToSearchItem(WalletViewModel wallet, HistoryItemViewModelBase item)
	{
		return new ActionableItem(
			item.Id.ToString(),
			@$"Found in ""{wallet.WalletName}""",
			() => NavigateTo(wallet, item),
			"Transactions",
			new List<string>())
		{
			Icon = GetIcon(item)
		};
	}

	private static IEnumerable<(WalletViewModel Wallet, IEnumerable<HistoryItemViewModelBase> Transactions)>
		GetTransactionsByWallet()
	{
		return UiServices.WalletManager.Wallets
			.Where(x => x.IsLoggedIn && x.WalletState == WalletState.Started)
			.OfType<WalletViewModel>()
			.Select(x => (Wallet: x, x.History.Transactions.Concat(x.History.Transactions.OfType<CoinJoinsHistoryItemViewModel>().SelectMany(y => y.Children))));
	}

	private IEnumerable<ISearchItem> PerformSearch(string s)
	{
		return Filter(s)
			.Take(5)
			.Select(tuple => ToSearchItem(tuple.Item1, tuple.Item2));
	}

	private IEnumerable<(WalletViewModel, HistoryItemViewModelBase)> Filter(string queryStr)
	{
		return Flatten(GetTransactionsByWallet())
			.Where(tuple => Contains(queryStr, tuple.Item2));
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
