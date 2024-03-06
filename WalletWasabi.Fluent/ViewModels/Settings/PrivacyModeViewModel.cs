using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Discreet Mode",
	Searchable = false,
	NavBarPosition = NavBarPosition.Bottom,
	NavBarSelectionMode = NavBarSelectionMode.Toggle)]
public partial class PrivacyModeViewModel : RoutableViewModel, IDisposable
{
	[AutoNotify] private bool _privacyMode;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;
	private readonly CompositeDisposable _disposables = new();

	public PrivacyModeViewModel(IApplicationSettings applicationSettings)
	{
		_privacyMode = applicationSettings.PrivacyMode;

		SetIcon();

		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.Do(x => applicationSettings.PrivacyMode = x)
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public void Toggle()
	{
		PrivacyMode = !PrivacyMode;
		SetIcon();
	}

	public void SetIcon()
	{
		IconName = PrivacyMode ? "eye_hide_regular" : "eye_show_regular";
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
