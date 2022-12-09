using System.Threading.Tasks;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

public enum NavBarItemSelectionMode
{
	Selected = 0,
	Button = 1,
	Toggle = 2
}

public abstract class NavBarItemViewModel : RoutableViewModel
{
	private readonly NavigationMode _defaultNavigationMode;
	private bool _isSelected;

	protected NavBarItemViewModel(NavigationMode defaultNavigationMode = NavigationMode.Clear)
	{
		_defaultNavigationMode = defaultNavigationMode;
		SelectionMode = NavBarItemSelectionMode.Selected;
		OpenCommand = ReactiveCommand.CreateFromTask<bool>(OnOpenCommandExecuted);
	}

	private async Task OnOpenCommandExecuted(bool enableReSelection = false)
	{
		if (!enableReSelection && IsSelected)
		{
			return;
		}

		IsSelected = true;
		await OnOpen(_defaultNavigationMode);
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
