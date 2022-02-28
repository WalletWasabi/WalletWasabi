using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

/// <summary>
/// The ViewModel that represents the structure of the sidebar.
/// </summary>
public partial class NavBarViewModel : ViewModelBase
{
	private const double NormalCompactPaneLength = 68;
	private const double NormalOpenPaneLength = 280;

	private NavBarItemViewModel? _selectedItem;
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

	public NavBarViewModel(TargettedNavigationStack mainScreen)
	{
		_topItems = new ObservableCollection<NavBarItemViewModel>();
		_bottomItems = new ObservableCollection<NavBarItemViewModel>();

		mainScreen.WhenAnyValue(x => x.CurrentPage)
			.WhereNotNull()
			.OfType<NavBarItemViewModel>()
			.DistinctUntilChanged()
			.Subscribe(CurrentPageChanged);

		this.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(selectedItem =>
			{
				if (selectedItem is { })
				{
					NavigateItem(selectedItem);
				}

				if (selectedItem is WalletViewModelBase wallet)
				{
					Services.UiConfig.LastSelectedWallet = wallet.WalletName;
				}
			});

		this.WhenAnyValue(x => x.Items.Count)
			.Subscribe(x =>
			{
				if (x > 0 && SelectedItem is null)
				{
					if (!UiServices.WalletManager.IsLoadingWallet)
					{
						var lastSelectedItem = Items.FirstOrDefault(item => item is WalletViewModelBase wallet && wallet.WalletName == Services.UiConfig.LastSelectedWallet);

						SelectedItem = lastSelectedItem ?? Items.FirstOrDefault();
					}
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
			.Subscribe(x =>
		   {
			   CurrentCompactPaneLength = x ? 0 : NormalCompactPaneLength;
			   CurrentOpenPaneLength = x ? 0 : NormalOpenPaneLength;
		   });

		UiServices.WalletManager.WhenAnyValue(x => x.SelectedWallet)
			.WhereNotNull()
			.OfType<NavBarItemViewModel>()
			.Subscribe(x => SelectedItem = x);
	}

	public ReadOnlyObservableCollection<NavBarItemViewModel> Items => UiServices.WalletManager.Items;

	public ObservableCollection<WalletViewModelBase> Wallets => UiServices.WalletManager.Wallets;

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
			var viewModel = await NavigationManager.MaterialiseViewModelAsync(item);

			if (viewModel is NavBarItemViewModel navBarItem)
			{
				_topItems.Add(navBarItem);
			}
		}

		foreach (var item in bottomItems)
		{
			var viewModel = await NavigationManager.MaterialiseViewModelAsync(item);

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
		}

		RaiseAndChangeSelectedItem(null);
		RaiseAndChangeSelectedItem(value);

		if (_selectedItem is { })
		{
			_selectedItem.IsSelected = true;
			_selectedItem.IsExpanded = IsOpen;
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

	private void CurrentPageChanged(NavBarItemViewModel x)
	{
		if (UiServices.WalletManager.Items.Contains(x) || _topItems.Contains(x) || _bottomItems.Contains(x))
		{
			if (!_isNavigating)
			{
				_isNavigating = true;

				var result = UiServices.WalletManager.SelectionChanged(x);
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

			var result = UiServices.WalletManager.SelectionChanged(x);
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
