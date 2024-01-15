using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class TransactionsSearchSource : ReactiveObject, ISearchSource, IDisposable
{
	private const int MaxResultCount = 5;
	private const int MinQueryLength = 3;

	private readonly CompositeDisposable _disposables = new();

	public TransactionsSearchSource(IObservable<string> queries)
	{
		var sourceCache = new SourceCache<ISearchItem, ComposedKey>(x => x.Key)
			.DisposeWith(_disposables);

		var results = queries
			.Select(query => query.Length >= MinQueryLength ? Search(query) : Enumerable.Empty<ISearchItem>())
			.ObserveOn(RxApp.MainThreadScheduler);

		sourceCache
			.RefillFrom(results)
			.DisposeWith(_disposables);

		Changes = sourceCache.Connect();
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }

	private static bool ContainsId(HistoryItemViewModelBase historyItemViewModelBase, string queryStr)
	{
		return historyItemViewModelBase.Transaction.Id.ToString().Contains(queryStr, StringComparison.CurrentCultureIgnoreCase);
	}

	private static Task NavigateTo(WalletViewModel wallet, HistoryItemViewModelBase item)
	{
		MainViewModel.Instance.NavBar.SelectedWallet = MainViewModel.Instance.NavBar.Wallets.FirstOrDefault(x => x.WalletViewModel == wallet);
		wallet.NavigateAndHighlight(item.Transaction.Id);
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

	private static IEnumerable<(WalletViewModel, HistoryItemViewModelBase)> Flatten(IEnumerable<(WalletViewModel Wallet, IEnumerable<HistoryItemViewModelBase> Transactions)> walletTransactions)
	{
		return walletTransactions.SelectMany(t => t.Transactions.Select(item => (t.Wallet, HistoryItem: item)));
	}

	private static ISearchItem ToSearchItem(WalletViewModel wallet, HistoryItemViewModelBase item)
	{
		return new ActionableItem(
			item.Transaction.Id.ToString(),
			@$"Found in ""{wallet.WalletModel.Name}""",
			() => NavigateTo(wallet, item),
			"Transactions",
			new List<string>())
		{
			Icon = GetIcon(item)
		};
	}

	private static IEnumerable<(WalletViewModel Wallet, IEnumerable<HistoryItemViewModelBase> Transactions)> GetTransactionsByWallet()
	{
		// TODO: This is a workaround to get all the transactions from currently loaded wallets. REMOVE after UIDecoupling #26

		return MainViewModel.Instance.NavBar.Wallets
			.Where(x => x.IsLoggedIn && x.Wallet.State == WalletState.Started)
			.Select(x => x.WalletViewModel)
			.WhereNotNull()
			.Select(
				x => (Wallet: x,
					x.History.Transactions.Concat(x.History.Transactions.OfType<CoinJoinsHistoryItemViewModel>().SelectMany(y => y.Children))));
	}

	private static IEnumerable<ISearchItem> Search(string query)
	{
		return Filter(query)
			.Take(MaxResultCount)
			.Select(tuple => ToSearchItem(tuple.Item1, tuple.Item2));
	}

	private static IEnumerable<(WalletViewModel, HistoryItemViewModelBase)> Filter(string queryStr)
	{
		return Flatten(GetTransactionsByWallet())
			.Where(tuple => ContainsId(tuple.Item2, queryStr));
	}
}
