using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Search;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SearchPageViewModel : NavBarItemViewModel
	{
		private readonly ReadOnlyObservableCollection<SearchItemGroup> _searchItemsByCategory;
		private string? _searchQuery;

		public SearchPageViewModel(NavigationStateViewModel navigationState, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Title = "Search";

			var searchItems = new SourceList<SearchItemViewModel>();

			var generalCategory = new SearchCategory("General", 0);
			var walletCategory = new SearchCategory("Wallet", 1);

			searchItems.Add(new SearchItemViewModel(
				navigationState,
				NavigationTarget.HomeScreen,
				iconName: "home_regular",
				title: "Home",
				caption: "Manage existing wallets",
				category: generalCategory,
				keywords: "Home",
				() => new HomePageViewModel(navigationState, walletManager, addWalletPage)));

			searchItems.Add(new SearchItemViewModel(
				navigationState,
				NavigationTarget.HomeScreen,
				iconName: "settings_regular",
				title: "Settings",
				caption: "Manage appearance, privacy and other settings",
				category: generalCategory,
				keywords: "Settings, General, User Interface, Privacy, Advanced",
				() => new SettingsPageViewModel(navigationState)));

			searchItems.Add(new SearchItemViewModel(
				navigationState,
				NavigationTarget.DialogScreen,
				iconName: "add_circle_regular",
				title: "Add Wallet",
				caption: "Create, recover or import wallet",
				category: generalCategory,
				keywords: "Wallet, Add Wallet, Create Wallet, Recover Wallet, Import Wallet, Connect Hardware Wallet",
				() => addWalletPage));

			var queryFilter = this.WhenValueChanged(t => t.SearchQuery)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.Select(SearchQueryFilter)
				.DistinctUntilChanged();

			walletManager.Items.ToObservableChangeSet()
				.Transform(x => new SearchItemViewModel(
					navigationState,
					NavigationTarget.HomeScreen,
					iconName: "web_asset_regular",
					title: x.WalletName,
					caption: "Wallet",
					category: walletCategory,
					keywords: $"Wallet, {x.WalletName}",
					() => x))
				.Sort(SortExpressionComparer<SearchItemViewModel>.Ascending(i => i.Title))
				.Merge(searchItems.Connect())
				.Filter(queryFilter)
				.GroupWithImmutableState(x => x.Category)
				.Transform(grouping => new SearchItemGroup(grouping.Key, grouping.Items.OrderBy(x => x.Title)))
				.Sort(SortExpressionComparer<SearchItemGroup>.Ascending(i => i.Category.Order).ThenByAscending(i => i.Category.Title))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _searchItemsByCategory)
				.AsObservableList();
		}

		public override string IconName => "search_regular";

		public string? SearchQuery
		{
			get => _searchQuery;
			set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
		}

		public ReadOnlyObservableCollection<SearchItemGroup> SearchItemsByCategory => _searchItemsByCategory;

		private Func<SearchItemViewModel, bool> SearchQueryFilter(string? searchQuery)
		{
			return item =>
			{
				if (!string.IsNullOrWhiteSpace(searchQuery)
				    && searchQuery.IndexOf(',', StringComparison.OrdinalIgnoreCase) < 0)
				{
					return item.Keywords.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
				}
				return true;
			};
		}
	}
}