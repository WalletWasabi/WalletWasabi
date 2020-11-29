using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Settings;
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
		
		public SearchPageViewModel(NavigationStateViewModel navigationState, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage, SettingsPageViewModel settingsPage, HomePageViewModel homePage) : base(navigationState)
		{
			Title = "Search";
			_categories = new Dictionary<string, SearchCategory>();
			_categorySources = new Dictionary<SearchCategory, SourceList<SearchItemViewModel>>();
			_walletManager = walletManager;

			RegisterCategory("General", 0);

			RegisterSearchEntry(
				"Home",
				"Manage existing wallets",
				0,
				"General",
				"Home",
				"home_regular",
				() => homePage);

			RegisterSearchEntry(
				title: "Settings",
				caption: "Manage appearance, privacy and other settings",
				order: 1,
				category: "General",
				keywords: "Settings, General, User Interface, Privacy, Advanced",
				iconName: "settings_regular",
				createTargetView: () => settingsPage);

			RegisterSearchEntry(
				title: "Add Wallet",
				caption: "Create, recover or import wallet",
				order: 2,
				category: "General",
				keywords: "Wallet, Add Wallet, Create Wallet, Recover Wallet, Import Wallet, Connect Hardware Wallet",
				iconName: "add_circle_regular",
				createTargetView: () => addWalletPage);

			RegisterCategory("Settings", 1);
			RegisterSettingsSearchItems(settingsPage);

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

		private void RegisterSettingsSearchItems(SettingsPageViewModel settingsPage)
		{
			RegisterSearchEntry(
				title: "General",
				caption: "Manage general settings",
				order: 0,
				category: "Settings",
				keywords: "Settings, General, Dark Mode, Bitcoin Addresses, Manual Entry Free, Custom Change Address, Fee Display Format, Dust Threshold, BTC",
				iconName: "settings_general_regular",
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 0;
					return settingsPage;
				});

			RegisterSearchEntry(
				title: "Privacy",
				caption: "Manage privacy settings",
				order: 1,
				category: "Settings",
				keywords: "Settings, Privacy, Minimal, Medium, Strong, Anonymity Level",
				iconName: "settings_privacy_regular",
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 1;
					return settingsPage;
				});

			RegisterSearchEntry(
				title: "Network",
				caption: "Manage network settings",
				order: 2,
				category: "Settings",
				keywords: "Settings, Network, Encryption, Tor, Terminate, Wasabi, Shutdown, SOCKS5, Endpoint",
				iconName: "settings_network_regular",
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 2;
					return settingsPage;
				});

			RegisterSearchEntry(
				title: "Bitcoin",
				caption: "Manage Bitcoin settings",
				order: 3,
				category: "Settings",
				keywords: "Settings, Bitcoin, Network, Main, TestNet, RegTest, Run, Knots, Startup, P2P, Endpoint",
				iconName: "settings_bitcoin_regular",
				createTargetView: () =>
				{
					settingsPage.SelectedTab = 3;
					return settingsPage;
				});
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
