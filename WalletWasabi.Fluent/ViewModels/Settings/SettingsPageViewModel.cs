using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public partial class SettingsPageViewModel : NavBarItemViewModel
	{
		[AutoNotify] private bool _isModified;
		[AutoNotify] private int _selectedTab;

		public SettingsPageViewModel(Config config, UiConfig uiConfig)
		{
			Title = "Settings";

			_selectedTab = 0;

			GeneralTab = new GeneralTabViewModel(config, uiConfig);
			PrivacyTab = new PrivacyTabViewModel(config, uiConfig);
			NetworkTab = new NetworkTabViewModel(config, uiConfig);
			BitcoinTab = new BitcoinTabViewModel(config, uiConfig);
		}

		public GeneralTabViewModel GeneralTab { get; }
		public PrivacyTabViewModel PrivacyTab { get; }
		public NetworkTabViewModel NetworkTab { get; }
		public BitcoinTabViewModel BitcoinTab { get; }

		public override string IconName => "settings_regular";

		private void OnRestartNeeded(object? sender, RestartNeededEventArgs e)
		{
			IsModified = e.IsRestartNeeded;
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			SettingsTabViewModelBase.RestartNeeded += OnRestartNeeded;

			Disposable.Create(() => { SettingsTabViewModelBase.RestartNeeded -= OnRestartNeeded; })
				.DisposeWith(disposable);
		}
	}
}