using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(Title = "Discreet Mode", Searchable = false)]
public partial class PrivacyModeViewModel : RoutableViewModel
{
	[AutoNotify] private bool _privacyMode;

	public PrivacyModeViewModel()
	{
		_privacyMode = Services.UiConfig.PrivacyMode;

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

	public void Toggle()
	{
		PrivacyMode = !PrivacyMode;
	}

	private void ToggleTitle()
	{
		Title = $"Discreet Mode {(_privacyMode ? "(On)" : "(Off)")}";
	}
}
