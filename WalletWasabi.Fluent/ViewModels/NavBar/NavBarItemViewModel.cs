using System.Threading.Tasks;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

public partial class NavBarItemViewModel1 : ViewModelBase
{
	private readonly INavBarItem _item;
	[AutoNotify] private string? _title;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;

	public NavBarItemViewModel1(INavBarItem item)
	{
		item.WhenAnyValue(x => x.Title)
			.BindTo(this, x => x.Title);

		item.WhenAnyValue(x => x.IconName)
			.BindTo(this, x => x.IconName);

		item.WhenAnyValue(x => x.IconNameFocused)
			.BindTo(this, x => x.IconNameFocused);
		_item = item;
	}

	public void Activate()
	{
		if (_item is INavBarToggle toggle)
		{
			toggle.Toggle();
		}
		if (_item is INavBarButton button)
		{
			button.Activate();
		}
	}
}

public abstract class NavBarItemViewModel : RoutableViewModel
{
	private readonly NavigationMode _defaultNavigationMode;
	private bool _isSelected;

	protected NavBarItemViewModel(NavigationMode defaultNavigationMode = NavigationMode.Clear)
	{
		_defaultNavigationMode = defaultNavigationMode;
		SelectionMode = NavBarItemSelectionMode.Selected;
		OpenCommand = ReactiveCommand.CreateFromTask<bool>(OnOpenCommandExecutedAsync);
	}

	public NavBarItemSelectionMode SelectionMode { get; protected init; }

	public bool IsSelectable => SelectionMode == NavBarItemSelectionMode.Selected;

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			switch (SelectionMode)
			{
				case NavBarItemSelectionMode.Selected:
					this.RaiseAndSetIfChanged(ref _isSelected, value);
					break;

				case NavBarItemSelectionMode.Button:
				case NavBarItemSelectionMode.Toggle:
					break;
			}
		}
	}

	public ICommand OpenCommand { get; }

	private async Task OnOpenCommandExecutedAsync(bool enableReSelection = false)
	{
		if (!enableReSelection && IsSelected)
		{
			return;
		}

		IsSelected = true;
		await OnOpen(_defaultNavigationMode);
	}

	protected virtual Task OnOpen(NavigationMode defaultNavigationMode)
	{
		if (SelectionMode == NavBarItemSelectionMode.Toggle)
		{
			Toggle();
		}
		else
		{
			Navigate().To(this, defaultNavigationMode);
		}

		return Task.CompletedTask;
	}

	public virtual void Toggle()
	{
	}
}
