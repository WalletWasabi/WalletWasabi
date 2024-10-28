using ReactiveUI;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

public partial class NavBarItemViewModel : RoutableViewModel
{
	private readonly INavBarItem _item;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;

	public NavBarItemViewModel(INavBarItem item)
	{
		_item = item;

		item.WhenAnyValue(x => x.Title)
			.BindTo(this, x => x.Title);

		item.WhenAnyValue(x => x.IconName)
			.BindTo(this, x => x.IconName);

		item.WhenAnyValue(x => x.IconNameFocused)
			.BindTo(this, x => x.IconNameFocused);

		OpenCommand = ReactiveCommand.CreateFromTask(ActivateAsync);
	}

	public ICommand OpenCommand { get; }

	public async Task ActivateAsync()
	{
		if (_item is INavBarToggle toggle)
		{
			toggle.Toggle();
		}
		if (_item is INavBarButton button)
		{
			await button.Activate();
		}
	}
}
