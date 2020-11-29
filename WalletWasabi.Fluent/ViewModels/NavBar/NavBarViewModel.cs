using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Search;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.NavBar
{
	/// <summary>
	/// The ViewModel that represents the structure of the sidebar.
	/// </summary>
	public class NavBarViewModel : ViewModelBase
	{
		private ObservableCollection<NavBarItemViewModel> _topItems;
		private ObservableCollection<NavBarItemViewModel> _bottomItems;
		private NavBarItemViewModel? _selectedItem;
		private readonly WalletManagerViewModel _walletManager;
		private bool _isBackButtonVisible;
		private bool _isNavigating;
		private bool _isOpen;
		private Action? _toggleAction;
		private Action? _collapseOnClickAction;

		public NavBarViewModel(
			TargettedNavigationStack mainScreen,
			WalletManagerViewModel walletManager,
			AddWalletPageViewModel addWalletPage,
			SettingsPageViewModel settingsPage,
			PrivacyModeViewModel privacyMode)
		{
			_walletManager = walletManager;
			_topItems = new ObservableCollection<NavBarItemViewModel>();
			_bottomItems = new ObservableCollection<NavBarItemViewModel>();

			var homePage = new HomePageViewModel(walletManager, addWalletPage);
			var searchPage = new SearchPageViewModel(walletManager);

			RegisterCategories(searchPage);

			RegisterRootEntries(searchPage, homePage, settingsPage, addWalletPage);

			RegisterEntries(searchPage);

			RegisterSettingsSearchItems(searchPage, settingsPage);

			searchPage.Initialise();

			_selectedItem = homePage;

			_topItems.Add(_selectedItem);
			_bottomItems.Add(searchPage);
			_bottomItems.Add(privacyMode);
			_bottomItems.Add(addWalletPage);
			_bottomItems.Add(settingsPage);

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<NavBarItemViewModel>()
				.Subscribe(NavigateItem);

			this.WhenAnyValue(x => x.IsOpen)
				.Subscribe(x => SelectedItem.IsExpanded = x);

			mainScreen.To(homePage);
		}

		public ObservableCollection<NavBarItemViewModel> TopItems
		{
			get => _topItems;
			set => this.RaiseAndSetIfChanged(ref _topItems, value);
		}

		public ObservableCollection<WalletViewModelBase> Items => _walletManager.Items;

		public ObservableCollection<NavBarItemViewModel> BottomItems
		{
			get => _bottomItems;
			set => this.RaiseAndSetIfChanged(ref _bottomItems, value);
		}

		public NavBarItemViewModel? SelectedItem
		{
			get => _selectedItem;
			set => SetSelectedItem(value);
		}

		public Action? ToggleAction
		{
			get => _toggleAction;
			set => this.RaiseAndSetIfChanged(ref _toggleAction, value);
		}

		public Action? CollapseOnClickAction
		{
			get => _collapseOnClickAction;
			set => this.RaiseAndSetIfChanged(ref _collapseOnClickAction, value);
		}

		public bool IsBackButtonVisible
		{
			get => _isBackButtonVisible;
			set => this.RaiseAndSetIfChanged(ref _isBackButtonVisible, value);
		}

		public bool IsOpen
		{
			get => _isOpen;
			set => this.RaiseAndSetIfChanged(ref _isOpen, value);
		}

		public void DoToggleAction()
		{
			ToggleAction?.Invoke();
		}

		private void RaiseAndChangeSelectedItem(NavBarItemViewModel? value)
		{
			_selectedItem = value;
			this.RaisePropertyChanged(nameof(SelectedItem));
		}

		private void Select(NavBarItemViewModel? value)
		{
			if (_selectedItem == value)
			{
				return;
			}

			if (_selectedItem is { })
			{
				_selectedItem.IsSelected = false;
				_selectedItem.IsExpanded = false;

				if (_selectedItem.Parent is { })
				{
					_selectedItem.Parent.IsSelected = false;
					_selectedItem.Parent.IsExpanded = false;
				}
			}

			RaiseAndChangeSelectedItem(null);
			RaiseAndChangeSelectedItem(value);

			if (_selectedItem is { })
			{
				_selectedItem.IsSelected = true;
				_selectedItem.IsExpanded = IsOpen;

				if (_selectedItem.Parent is { })
				{
					_selectedItem.Parent.IsSelected = true;
					_selectedItem.Parent.IsExpanded = true;
				}
			}
		}

		private void SetSelectedItem(NavBarItemViewModel? value)
		{
			if (value is null || value.SelectionMode == NavBarItemSelectionMode.Selected)
			{
				Select(value);
				return;
			}

			if (value.SelectionMode == NavBarItemSelectionMode.Button)
			{
				_isNavigating = true;
				var previous = _selectedItem;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(value);
				_isNavigating = false;
				NavigateItem(value);
				_isNavigating = true;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(previous);
				_isNavigating = false;
				return;
			}

			if (value.SelectionMode == NavBarItemSelectionMode.Toggle)
			{
				_isNavigating = true;
				var previous = _selectedItem;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(value);
				_isNavigating = false;
				value.Toggle();
				_isNavigating = true;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(previous);
				_isNavigating = false;
			}
		}

		private void SelectItem(NavBarItemViewModel x, WalletManagerViewModel walletManager)
		{
			if (walletManager.Items.Contains(x) || _topItems.Contains(x) || _bottomItems.Contains(x))
			{
				if (!_isNavigating)
				{
					_isNavigating = true;
					SetSelectedItem(x);
					_isNavigating = false;
				}
			}
		}

		private void NavigateItem(NavBarItemViewModel x)
		{
			/*if (!_isNavigating)
			{
				_isNavigating = true;
				if (x.OpenCommand.CanExecute(default))
				{
					x.OpenCommand.Execute(default);
				}

				CollapseOnClickAction?.Invoke();
				_isNavigating = false;
			}*/
		}

		private static void RegisterCategories(SearchPageViewModel searchPage)
		{
			searchPage.RegisterCategory("General", 0);
			searchPage.RegisterCategory("Settings", 1);
		}

		private static void RegisterEntries(SearchPageViewModel searchPage)
		{
			searchPage.RegisterSearchEntry(
				title: "About Wasabi",
				caption: "Displays all the current info about the app",
				order: 3,
				category: "General",
				keywords: "About, Software, Version, Source Code, Github, Status, Stats, Tor, Onion, Bug, Report, FAQ, Questions," +
				          "Docs, Documentation, Link, Links, Help",
				iconName: "info_regular",
				createTargetView: () => new AboutViewModel());
		}

		private static void RegisterRootEntries(
			SearchPageViewModel searchPage,
			HomePageViewModel homePage,
			SettingsPageViewModel settingsPage,
			AddWalletPageViewModel addWalletPage)
		{
			searchPage.RegisterSearchEntry(
				"Home",
				"Manage existing wallets",
				0,
				"General",
				"Home",
				"home_regular",
				() => homePage);

			searchPage.RegisterSearchEntry(
				title: "Settings",
				caption: "Manage appearance, privacy and other settings",
				order: 1,
				category: "General",
				keywords: "Settings, General, User Interface, Privacy, Advanced",
				iconName: "settings_regular",
				createTargetView: () => settingsPage);

			searchPage.RegisterSearchEntry(
				title: "Add Wallet",
				caption: "Create, recover or import wallet",
				order: 2,
				category: "General",
				keywords: "Wallet, Add Wallet, Create Wallet, Recover Wallet, Import Wallet, Connect Hardware Wallet",
				iconName: "add_circle_regular",
				createTargetView: () => addWalletPage);
		}

		private static void RegisterSettingsSearchItems(SearchPageViewModel searchPage, SettingsPageViewModel settingsPage)
		{
			searchPage.RegisterSearchEntry(
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

			searchPage.RegisterSearchEntry(
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

			searchPage.RegisterSearchEntry(
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

			searchPage.RegisterSearchEntry(
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
	}
}
