using System.Reactive;
using System.Reactive.Disposables;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Settings",
	Caption = "Manage appearance, privacy and other settings",
	Order = 1,
	Category = "General",
	Keywords = new[] { "Settings", "General", "User", "Interface", "Advanced" },
	IconName = "nav_settings_24_regular",
	IconNameFocused = "nav_settings_24_filled",
	Searchable = false,
	NavBarPosition = NavBarPosition.Bottom,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SettingsPageViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private bool _isModified;
	[AutoNotify] private int _selectedTab;

	public SettingsPageViewModel()
	{
		_selectedTab = 0;
		SelectionMode = NavBarItemSelectionMode.Button;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		GeneralSettingsTab = new GeneralSettingsTabViewModel();
		BitcoinTabSettings = new BitcoinTabSettingsViewModel();

		RestartCommand = ReactiveCommand.Create(AppLifetimeHelper.Restart);
		NextCommand = CancelCommand;
	}

	public ICommand RestartCommand { get; }

	public GeneralSettingsTabViewModel GeneralSettingsTab { get; }
	public BitcoinTabSettingsViewModel BitcoinTabSettings { get; }

	private void OnRestartNeeded(object? sender, RestartNeededEventArgs e)
	{
		IsModified = e.IsRestartNeeded;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		IsModified = SettingsTabViewModelBase.CheckIfRestartIsNeeded();

		SettingsTabViewModelBase.RestartNeeded += OnRestartNeeded;

		disposables.Add(
			Disposable.Create(() => SettingsTabViewModelBase.RestartNeeded -= OnRestartNeeded));
	}
}
