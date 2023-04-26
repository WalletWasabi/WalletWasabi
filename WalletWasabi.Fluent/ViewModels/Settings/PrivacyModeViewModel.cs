using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(Title = "Discreet Mode", Searchable = false, NavBarPosition = NavBarPosition.Bottom, NavBarSelectionMode = NavBarSelectionMode.Toggle)]
public partial class PrivacyModeViewModel : ViewModelBase
{
	[AutoNotify] private bool _privacyMode;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;

	public PrivacyModeViewModel()
	{
		_privacyMode = Services.UiConfig.PrivacyMode;

		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x => Services.UiConfig.PrivacyMode = x);
	}

	public void Toggle()
	{
		PrivacyMode = !PrivacyMode;
	}

	public void SetTitleAndIcon()
	{
		IconName = PrivacyMode ? "nav_incognito_24_filled" : "nav_incognito_24_regular";
		Title = $"Discreet Mode {(PrivacyMode ? "(On)" : "(Off)")}";
	}
}
