using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(Title = "Discreet Mode", Searchable = false, NavBarPosition = NavBarPosition.Bottom)]
public partial class PrivacyModeViewModel : NavBarItemViewModel
{
	[AutoNotify] private bool _privacyMode;

	public PrivacyModeViewModel()
	{
		_privacyMode = Services.UiConfig.PrivacyMode;

		SelectionMode = NavBarItemSelectionMode.Toggle;

		ToggleTitle();

		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
			{
				ToggleTitle();
				this.RaisePropertyChanged(nameof(IconName));
				Services.UiConfig.PrivacyMode = x;
			});
	}

	public override string IconName => _privacyMode ? "nav_incognito_24_filled" : "nav_incognito_24_regular";

	public override void Toggle()
	{
		PrivacyMode = !PrivacyMode;
	}

	private void ToggleTitle()
	{
		Title = $"Discreet Mode {(_privacyMode ? "(On)" : "(Off)")}";
	}
}
