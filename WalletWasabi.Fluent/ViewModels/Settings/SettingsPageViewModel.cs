using System.Reactive.Disposables;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	[NavigationMetaData(
		Title = "Settings",
		Caption = "Manage appearance, privacy and other settings",
		Order = 1,
		Category = "General",
		Keywords = new[] { "Settings", "General", "User", "Interface", "Privacy", "Advanced" },
		IconName = "settings_regular")]
	public partial class SettingsPageViewModel : NavBarItemViewModel
	{
		[AutoNotify] private bool _isModified;
		[AutoNotify] private int _selectedTab;

		public SettingsPageViewModel()
		{
			_selectedTab = 0;

			GeneralSettingsTab = new GeneralSettingsTabViewModel();
			PrivacySettingsTab = new PrivacySettingsTabViewModel();
			NetworkSettingsTab = new NetworkSettingsTabViewModel();
			BitcoinTabSettings = new BitcoinTabSettingsViewModel();
		}

		public GeneralSettingsTabViewModel GeneralSettingsTab { get; }
		public PrivacySettingsTabViewModel PrivacySettingsTab { get; }
		public NetworkSettingsTabViewModel NetworkSettingsTab { get; }
		public BitcoinTabSettingsViewModel BitcoinTabSettings { get; }

		private void OnRestartNeeded(object? sender, RestartNeededEventArgs e)
		{
			IsModified = e.IsRestartNeeded;
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			SettingsTabViewModelBase.RestartNeeded += OnRestartNeeded;

			Disposable.Create(() => SettingsTabViewModelBase.RestartNeeded -= OnRestartNeeded)
				.DisposeWith(disposables);
		}
	}
}
