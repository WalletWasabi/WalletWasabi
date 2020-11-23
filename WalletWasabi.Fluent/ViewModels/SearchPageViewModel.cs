using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Search;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SearchPageViewModel : NavBarItemViewModel
	{
		private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
		private string? _searchQuery;

		public SearchPageViewModel(NavigationStateViewModel navigationState, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Title = "Search";

			var searchItems = new SourceList<SearchItemViewModel>();
			var generalCategory = new SearchCategory("General", 0);
			var walletCategory = new SearchCategory("Wallets", 1);

			searchItems.Add(
				CreateHomeSearchItem(generalCategory, 0, walletManager, addWalletPage));

			searchItems.Add(
				CreateSettingsSearchItem(generalCategory, 1));

			searchItems.Add(
				CreateAddWalletSearchItem(generalCategory, 2, addWalletPage));

			var queryFilter = this.WhenValueChanged(t => t.SearchQuery)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.Select(SearchQueryFilter)
				.DistinctUntilChanged();

			walletManager.Items.ToObservableChangeSet()
				.Transform(x => CreateWalletSearchItem(walletCategory, 0, x))
				.Sort(SortExpressionComparer<SearchItemViewModel>.Ascending(i => i.Title))
				.Merge(searchItems.Connect())
				.Filter(queryFilter)
				.GroupWithImmutableState(x => x.Category)
				.Transform(grouping => new SearchResult(grouping.Key, grouping.Items.OrderBy(x => x.Order).ThenBy(x => x.Title)))
				.Sort(SortExpressionComparer<SearchResult>.Ascending(i => i.Category.Order).ThenByAscending(i => i.Category.Title))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _searchResults)
				.AsObservableList();
		}

		public override string IconName => "search_regular";

		public string? SearchQuery
		{
			get => _searchQuery;
			set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
		}

		public ReadOnlyObservableCollection<SearchResult> SearchResults => _searchResults;

		private Func<SearchItemViewModel, bool> SearchQueryFilter(string? searchQuery)
		{
			return item =>
			{
				if (!string.IsNullOrWhiteSpace(searchQuery)
				    && searchQuery.IndexOf(',', StringComparison.OrdinalIgnoreCase) < 0)
				{
					return item.Keywords.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
					       item.Caption.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
				}
				return true;
			};
		}

		private SearchItemViewModel CreateHomeSearchItem(SearchCategory category, int order, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage)
		{
			return new(
				title: "Home",
				caption: "Manage existing wallets",
				order: order,
				category: category,
				keywords: "Home",
				iconName: "home_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () => new HomePageViewModel(NavigationState, walletManager, addWalletPage));
		}

		private SearchItemViewModel CreateSettingsSearchItem(SearchCategory category, int order)
		{
			return new(
				title: "Settings",
				caption: "Manage appearance, privacy and other settings",
				order: order,
				category: category,
				keywords: "Settings, General, User Interface, Privacy, Advanced",
				iconName: "settings_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () => new SettingsPageViewModel(NavigationState));
		}

		private SearchItemViewModel CreateAddWalletSearchItem(SearchCategory category, int order, AddWalletPageViewModel addWalletPage)
		{
			return new(
				title: "Add Wallet",
				caption: "Create, recover or import wallet",
				order: order,
				category: category,
				keywords: "Wallet, Add Wallet, Create Wallet, Recover Wallet, Import Wallet, Connect Hardware Wallet",
				iconName: "add_circle_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.DialogScreen,
				createTargetView: () => addWalletPage);
		}

		private SearchItemViewModel CreateWalletSearchItem(SearchCategory category, int order, WalletViewModelBase wallet)
		{
			return new(
				title: wallet.WalletName,
				caption: "",
				order: order,
				category: category,
				keywords: $"Wallet, {wallet.WalletName}",
				iconName: "web_asset_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () => wallet);
		}
	}
}
