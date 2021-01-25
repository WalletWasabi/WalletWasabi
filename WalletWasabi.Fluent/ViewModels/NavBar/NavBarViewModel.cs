using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.NavBar
{
	/// <summary>
	/// The ViewModel that represents the structure of the sidebar.
	/// </summary>
	public partial class NavBarViewModel : ViewModelBase
	{
		private const double NormalCompactPaneLength = 68;
		private const double NormalOpenPaneLength = 280;

		private NavBarItemViewModel? _selectedItem;
		private readonly WalletManagerViewModel _walletManager;
		private bool _isNavigating;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _topItems;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _bottomItems;
		[AutoNotify] private bool _isBackButtonVisible;
		[AutoNotify] private bool _isOpen;
		[AutoNotify] private Action? _toggleAction;
		[AutoNotify] private Action? _collapseOnClickAction;
		[AutoNotify] private double _currentOpenPaneLength;
		[AutoNotify] private double _currentCompactPaneLength;
		[AutoNotify] private bool _isHidden;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _hideItems;

		public NavBarViewModel(TargettedNavigationStack mainScreen, WalletManagerViewModel walletManager)
		{
			_walletManager = walletManager;
			_topItems = new ObservableCollection<NavBarItemViewModel>();
			_bottomItems = new ObservableCollection<NavBarItemViewModel>();

			mainScreen.WhenAnyValue(x => x.CurrentPage)
				.OfType<NavBarItemViewModel>()
				.DistinctUntilChanged()
				.Subscribe(x => CurrentPageChanged(x, walletManager));

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<NavBarItemViewModel>()
				.Subscribe(NavigateItem);

			this.WhenAnyValue(x => x.Items.Count)
				.Subscribe(x =>
				{
					if (x > 0 && SelectedItem is null)
					{
						SelectedItem = Items.FirstOrDefault();
					}
				});

			this.WhenAnyValue(x => x.IsOpen)
				.Subscribe(x =>
				{
					if (SelectedItem is { })
					{
						SelectedItem.IsExpanded = x;
					}
				});

			this.WhenAnyValue(x => x.IsHidden)
				.Subscribe(
					x =>
				{
					CurrentCompactPaneLength = x ? 0 : NormalCompactPaneLength;
					CurrentOpenPaneLength = x ? 0 : NormalOpenPaneLength;
				});

			this.WhenAnyValue(x => x.IsOpen, x => x.Actions.Count)
				.Subscribe(x =>
				{
					HideItems = !x.Item1 && x.Item2 > 0;
				});

			_walletManager.WhenAnyValue(x => x.SelectedWallet)
				.OfType<NavBarItemViewModel>()
				.Subscribe(x =>
				{
					if (x is not null)
					{
						SelectedItem = x;
					}
				});
		}

		public ObservableCollection<NavBarItemViewModel> Actions => _walletManager.Actions;

		public ReadOnlyObservableCollection<NavBarItemViewModel> Items => _walletManager.Items;

		public NavBarItemViewModel? SelectedItem
		{
			get => _selectedItem;
			set => SetSelectedItem(value);
		}

		public async Task InitialiseAsync()
		{
			var topItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Top);

			var bottomItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Bottom);

			foreach (var item in topItems)
			{
				var viewModel = await NavigationManager.MaterialiseViewModel(item);

				if (viewModel is NavBarItemViewModel navBarItem)
				{
					_topItems.Add(navBarItem);
				}
			}

			foreach (var item in bottomItems)
			{
				var viewModel = await NavigationManager.MaterialiseViewModel(item);

				if (viewModel is NavBarItemViewModel navBarItem)
				{
					_bottomItems.Add(navBarItem);
				}
			}
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

		private void CurrentPageChanged(NavBarItemViewModel x, WalletManagerViewModel walletManager)
		{
			if (walletManager.Items.Contains(x) || _topItems.Contains(x) || _bottomItems.Contains(x))
			{
				if (!_isNavigating)
				{
					_isNavigating = true;

					var result = walletManager.SelectionChanged(x);
					if (result is not null)
					{
						SelectedItem = x;
					}

					if (x.SelectionMode == NavBarItemSelectionMode.Selected)
					{
						SetSelectedItem(x);
					}

					_isNavigating = false;
				}
			}
		}

		private void NavigateItem(NavBarItemViewModel x)
		{
			if (!_isNavigating)
			{
				_isNavigating = true;

				var result = _walletManager.SelectionChanged(x);
				if (result is not null)
				{
					SelectedItem = result;
				}

				if (x.OpenCommand.CanExecute(default))
				{
					x.OpenCommand.Execute(default);
				}

				CollapseOnClickAction?.Invoke();
				_isNavigating = false;
			}
		}
	}
}
