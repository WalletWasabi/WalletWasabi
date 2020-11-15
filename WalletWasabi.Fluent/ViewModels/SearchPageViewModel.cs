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
		private string? _searchQuery;
		private readonly ReadOnlyObservableCollection<SearchItemViewModel> _searchItems;
		private readonly ReadOnlyObservableCollection<SearchItemGroup> _searchItemsByCategory;

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
				category: generalCategory,
				keywords: "Home",
				() => new HomePageViewModel(navigationState, walletManager, addWalletPage)));

			searchItems.Add(new SearchItemViewModel(
				navigationState,
				NavigationTarget.HomeScreen,
				iconName: "settings_regular",
				title: "Settings",
				category: generalCategory,
				keywords: "Settings, General, User Interface, Privacy, Advanced",
				() => new SettingsPageViewModel(navigationState)));

			searchItems.Add(new SearchItemViewModel(
				navigationState,
				NavigationTarget.DialogScreen,
				iconName: "add_circle_regular",
				title: "Add Wallet",
				category: generalCategory,
				keywords: "Wallet, Add Wallet, Create Wallet, Recover Wallet, Import Wallet, Connect Hardware Wallet",
				() => addWalletPage));

			var filter = this.WhenValueChanged(t => t.SearchQuery)
				.Throttle(TimeSpan.FromMilliseconds(250))
				.Select(SearchQueryFilter)
				.DistinctUntilChanged();

			var observable = walletManager.Items.ToObservableChangeSet()
				.Transform(x => new SearchItemViewModel(
					navigationState,
					NavigationTarget.HomeScreen,
					iconName: "web_asset_regular",
					title: x.WalletName,
					category: walletCategory,
					keywords: $"Wallet, {x.WalletName}",
					() => x))
				.Sort(SortExpressionComparer<SearchItemViewModel>.Ascending(i => i.Title))
				.Merge(searchItems.Connect())
				.Filter(filter)
				.ObserveOn(RxApp.MainThreadScheduler);

			observable.Bind(out _searchItems)
				.AsObservableList();

			observable.GroupWithImmutableState(x => x.Category)
				.Transform(grouping => new SearchItemGroup(grouping.Key, grouping.Items.OrderBy(x => x.Title)))
				.Sort(SortExpressionComparer<SearchItemGroup>.Ascending(i => i.Category.Order).ThenByAscending(i => i.Category.Title))
				.Bind(out _searchItemsByCategory)
				.AsObservableList();
		}

		private static Func<SearchItemViewModel, bool> SearchQueryFilter(string? searchQuery)
		{
			return item =>
			{
				if (!string.IsNullOrWhiteSpace(searchQuery)
				    && searchQuery.IndexOf(',', StringComparison.OrdinalIgnoreCase) < 0)
				{
					if (item.Keywords.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return true;
					}
					return false;
				}
				return true;
			};
		}

		public override string IconName => "search_regular";

		public string? SearchQuery
		{
			get => _searchQuery;
			set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
		}

		public ReadOnlyObservableCollection<SearchItemViewModel> SearchItems => _searchItems;

		public ReadOnlyObservableCollection<SearchItemGroup> SearchItemsByCategory => _searchItemsByCategory;
	}
}