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

		public NavBarViewModel(NavigationStateViewModel navigationState, RoutingState router, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage)
		{
			Router = router;
			_walletManager = walletManager;
			_topItems = new ObservableCollection<NavBarItemViewModel>();
			_bottomItems = new ObservableCollection<NavBarItemViewModel>();

			var homePage = new HomePageViewModel(navigationState, walletManager, addWalletPage);
			var settingsPage = new SettingsPageViewModel(navigationState);
			var searchPage = new SearchPageViewModel(navigationState, walletManager, addWalletPage, settingsPage, homePage);

			// Register categories and entries here.

			searchPage.Initialise();

			_selectedItem = homePage;

			_topItems.Add(SelectedItem);
			_bottomItems.Add(searchPage);
			_bottomItems.Add(settingsPage);
			_bottomItems.Add(addWalletPage);

			Router.CurrentViewModel
				.OfType<NavBarItemViewModel>()
				.Subscribe(
					x =>
				{
					if (walletManager.Items.Contains(x) || _topItems.Contains(x) || _bottomItems.Contains(x))
					{
						if (!_isNavigating)
						{
							_isNavigating = true;
							SelectedItem = x;
							_isNavigating = false;
						}
					}
				});

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<NavBarItemViewModel>()
				.Subscribe(
					x =>
				{
					if (!_isNavigating)
					{
						_isNavigating = true;
						x.NavigateToSelfAndReset(x.CurrentTarget);
						CollapseOnClickAction?.Invoke();

						_isNavigating = false;
					}
				});

			Observable.FromEventPattern(Router.NavigationStack, nameof(Router.NavigationStack.CollectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => IsBackButtonVisible = Router.NavigationStack.Count > 1);

			this.WhenAnyValue(x => x.IsOpen)
				.Subscribe(x => SelectedItem.IsExpanded = x);
		}

		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

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
			set
			{
				if (_selectedItem != value)
				{
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

					_selectedItem = null;

					this.RaisePropertyChanged();

					_selectedItem = value;

					this.RaisePropertyChanged();

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
			}
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

		public RoutingState Router { get; }

		public void DoToggleAction()
		{
			ToggleAction?.Invoke();
		}
	}
}