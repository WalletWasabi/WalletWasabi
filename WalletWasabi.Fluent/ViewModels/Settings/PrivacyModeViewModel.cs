using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Discreet Mode",
	Searchable = false,
	NavBarPosition = NavBarPosition.Bottom,
	NavBarSelectionMode = NavBarSelectionMode.Toggle)]
public partial class PrivacyModeViewModel : RoutableViewModel
{
	[AutoNotify] private bool _privacyMode;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;

	public PrivacyModeViewModel()
	{
		_privacyMode = Services.UiConfig.PrivacyMode;

		SetIcon();

		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x => Services.UiConfig.PrivacyMode = x);
	}

	public void Toggle()
	{
		PrivacyMode = !PrivacyMode;
		SetIcon();
	}

	public void SetIcon()
	{
		IconName = PrivacyMode ? "nav_incognito_24_filled" : "nav_incognito_24_regular";
	}
}
