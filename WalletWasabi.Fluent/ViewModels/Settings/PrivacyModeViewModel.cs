using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(Searchable = false, NavBarPosition = NavBarPosition.Bottom, NavBarSelectionMode = NavBarSelectionMode.Toggle)]
public partial class PrivacyModeViewModel : RoutableViewModel
{
	private string _title = "";
	[AutoNotify] private bool _privacyMode;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;

	public PrivacyModeViewModel()
	{
		_privacyMode = Services.UiConfig.PrivacyMode;

		SetTitleAndIcon();

		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x => Services.UiConfig.PrivacyMode = x);
	}

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	public void Toggle()
	{
		PrivacyMode = !PrivacyMode;
		SetTitleAndIcon();
	}

	public void SetTitleAndIcon()
	{
		IconName = PrivacyMode ? "nav_incognito_24_filled" : "nav_incognito_24_regular";
		Title = $"Discreet Mode {(PrivacyMode ? "(On)" : "(Off)")}";
	}
}
