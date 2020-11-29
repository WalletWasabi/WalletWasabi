using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchPageViewModel : NavBarItemViewModel
	{
		private readonly bool _showWallets = false;
		private readonly WalletManagerViewModel _walletManager;
		private readonly Dictionary<string, SearchCategory> _categories;
		private readonly Dictionary<SearchCategory, SourceList<SearchItemViewModel>> _categorySources;
		private ReadOnlyObservableCollection<SearchResult>? _searchResults;
		private IObservable<IChangeSet<SearchItemViewModel>>? _sourceObservable;
		private string? _searchQuery;

		public SearchPageViewModel(NavigationStateViewModel navigationState, WalletManagerViewModel walletManager) : base(navigationState)
		{
			Title = "Search";
			_categories = new Dictionary<string, SearchCategory>();
			_categorySources = new Dictionary<SearchCategory, SourceList<SearchItemViewModel>>();
			_walletManager = walletManager;

			RegisterCategory("Wallets", 2);
		}

		public override string IconName => "search_regular";

		public string? SearchQuery
		{
			get => _searchQuery;
			set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
		}

		public ReadOnlyObservableCollection<SearchResult> SearchResults => _searchResults;

		public void Initialise()
		{
			if (_showWallets)
			{
				_walletManager.Items
					.ToObservableChangeSet()
					.OnItemAdded(x => RegisterWalletSearchItem(0, x))
					.Subscribe();
			}

			var queryFilter = this.WhenValueChanged(t => t.SearchQuery)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.Select(SearchQueryFilter)
				.DistinctUntilChanged();

			_sourceObservable
				.Filter(queryFilter)
				.GroupWithImmutableState(x => x.Category)
				.Transform(grouping => new SearchResult(grouping.Key, grouping.Items.OrderBy(x => x.Order).ThenBy(x => x.Title)))
				.Sort(SortExpressionComparer<SearchResult>.Ascending(i => i.Category.Order).ThenByAscending(i => i.Category.Title))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _searchResults)
				.AsObservableList();
		}

		public SearchCategory RegisterCategory(string title, int order)
		{
			if (!_categories.ContainsKey(title))
			{
				var category = new SearchCategory(title, order);

				_categories.Add(title, category);

				var sourceList = new SourceList<SearchItemViewModel>();

				_categorySources.Add(category, sourceList);

				if (_sourceObservable is null)
				{
					_sourceObservable = sourceList.Connect();
				}
				else
				{
					_sourceObservable = _sourceObservable.Merge(sourceList.Connect());
				}

				return category;
			}

			throw new Exception("Category already exists.");
		}

		public SearchItemViewModel RegisterSearchEntry(
			string title,
			string caption,
			int order,
			string category,
			string keywords,
			string iconName,
			Func<RoutableViewModel> createTargetView)
		{
			if (_categories.TryGetValue(category, out var searchCategory))
			{
				var result = new SearchItemViewModel(
					title,
					caption,
					order,
					searchCategory,
					keywords,
					iconName,
					NavigationState,
					createTargetView);

				_categorySources[searchCategory].Add(result);

				return result;
			}

			throw new Exception("Category doesnt exist.");
		}

		private Func<SearchItemViewModel, bool> SearchQueryFilter(string? searchQuery)
		{
			return item =>
			{
				if (!string.IsNullOrWhiteSpace(searchQuery)
				    && !searchQuery.Contains(',', StringComparison.OrdinalIgnoreCase))
				{
					return item.Keywords.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
					       item.Caption.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
				}
				return true;
			};
		}

		private void RegisterWalletSearchItem(int order, WalletViewModelBase wallet)
		{
			RegisterSearchEntry(
				title: wallet.WalletName,
				caption: "",
				order: order,
				category: "Wallets",
				keywords: $"Wallet, {wallet.WalletName}",
				iconName: "web_asset_regular",
				createTargetView: () => wallet);
		}
	}
}
