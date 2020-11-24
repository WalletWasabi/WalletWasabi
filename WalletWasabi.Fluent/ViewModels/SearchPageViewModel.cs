using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Search;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SearchPageViewModel : NavBarItemViewModel
	{
		private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
		private string? _searchQuery;
		private readonly bool _showSettings;
		private readonly bool _showWallets;

		public SearchPageViewModel(NavigationStateViewModel navigationState, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage, SettingsPageViewModel settingsPage, HomePageViewModel homePage) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Title = "Search";

			_showSettings = true;
			_showWallets = false;

			var generalCategory = new SearchCategory("General", 0);
			var generalCategorySource = new SourceList<SearchItemViewModel>();
			generalCategorySource.Add(CreateHomeSearchItem(generalCategory, 0, homePage));
			generalCategorySource.Add(CreateSettingsSearchItem(generalCategory, 1, settingsPage));
			generalCategorySource.Add(CreateAddWalletSearchItem(generalCategory, 2, addWalletPage));

			var settingsCategory = new SearchCategory("Settings", 1);
			var settingsCategorySource = new SourceList<SearchItemViewModel>();
			settingsCategorySource.Add(CreateGeneralSettingsSearchItem(settingsCategory, 0, settingsPage));
			settingsCategorySource.Add(CreatePrivacySettingsSearchItem(settingsCategory, 1, settingsPage));
			settingsCategorySource.Add(CreateNetworkSettingsSearchItem(settingsCategory, 2, settingsPage));
			settingsCategorySource.Add(CreateBitcoinSettingsSearchItem(settingsCategory, 3, settingsPage));

			var walletCategory = new SearchCategory("Wallets", 2);
			var wallets = walletManager.Items
				.ToObservableChangeSet()
				.Transform(x => CreateWalletSearchItem(walletCategory, 0, x))
				.Sort(SortExpressionComparer<SearchItemViewModel>.Ascending(i => i.Title));

			var searchItems = generalCategorySource.Connect();

			if (_showSettings)
			{
				searchItems.Merge(settingsCategorySource.Connect());
			}

			if (_showWallets)
			{
				searchItems.Merge(wallets);
			}

			var queryFilter = this.WhenValueChanged(t => t.SearchQuery)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.Select(SearchQueryFilter)
				.DistinctUntilChanged();

			searchItems
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
				    && !searchQuery.Contains(',', StringComparison.OrdinalIgnoreCase))
				{
					return item.Keywords.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
					       item.Caption.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
				}
				return true;
			};
		}

		private SearchItemViewModel CreateHomeSearchItem(SearchCategory category, int order, HomePageViewModel homePage)
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
				createTargetView: () => homePage);
		}

		private SearchItemViewModel CreateSettingsSearchItem(SearchCategory category, int order, SettingsPageViewModel settingsPage)
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
				createTargetView: () => settingsPage);
		}

		private SearchItemViewModel CreateGeneralSettingsSearchItem(SearchCategory category, int order, SettingsPageViewModel settingsPage)
		{
			return new(
				title: "General",
				caption: "Manage general settings",
				order: order,
				category: category,
				keywords: "Settings, General, Dark Mode, Bitcoin Addresses, Manual Entry Free, Custom Change Address, Fee Display Format, Dust Threshold, BTC",
				iconName: "settings_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 0;
					return settingsPage;
				});
		}

		private SearchItemViewModel CreatePrivacySettingsSearchItem(SearchCategory category, int order, SettingsPageViewModel settingsPage)
		{
			return new(
				title: "Privacy",
				caption: "Manage privacy settings",
				order: order,
				category: category,
				keywords: "Settings, Privacy, Minimal, Medium, Strong, Anonymity Level",
				iconName: "settings_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 1;
					return settingsPage;
				});
		}

		private SearchItemViewModel CreateNetworkSettingsSearchItem(SearchCategory category, int order, SettingsPageViewModel settingsPage)
		{
			return new(
				title: "Network",
				caption: "Manage network settings",
				order: order,
				category: category,
				keywords: "Settings, Network, Encryption, Tor, Terminate, Wasabi, Shutdown, SOCKS5, Endpoint",
				iconName: "settings_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 2;
					return settingsPage;
				});
		}

		private SearchItemViewModel CreateBitcoinSettingsSearchItem(SearchCategory category, int order, SettingsPageViewModel settingsPage)
		{
			return new(
				title: "Bitcoin",
				caption: "Manage Bitcoin settings",
				order: order,
				category: category,
				keywords: "Settings, Bitcoin, Network, Main, TestNet, RegTest, Run, Knots, Startup, P2P, Endpoint",
				iconName: "settings_regular",
				navigationState: NavigationState,
				navigationTarget: NavigationTarget.HomeScreen,
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 3;
					return settingsPage;
				});
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
