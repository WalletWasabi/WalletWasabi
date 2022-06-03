using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(Title = "Discreet Mode", Searchable = false, NavBarPosition = NavBarPosition.Bottom)]
public partial class DiscreetModeViewModel : NavBarItemViewModel
{
	[AutoNotify] private bool _discreetMode;

	public DiscreetModeViewModel()
	{
		_discreetMode = Services.UiConfig.DiscreetMode;

		SelectionMode = NavBarItemSelectionMode.Toggle;

		ToggleTitle();

		this.WhenAnyValue(x => x.DiscreetMode)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
			{
				ToggleTitle();
				this.RaisePropertyChanged(nameof(IconName));
				Services.UiConfig.DiscreetMode = x;
			});
	}

	public override string IconName => _discreetMode ? "nav_incognito_24_filled" : "nav_incognito_24_regular";

	public override void Toggle()
	{
		DiscreetMode = !DiscreetMode;
	}

	private void ToggleTitle()
	{
		Title = $"Discreet Mode {(_discreetMode ? "(On)" : "(Off)")}";
	}
}
