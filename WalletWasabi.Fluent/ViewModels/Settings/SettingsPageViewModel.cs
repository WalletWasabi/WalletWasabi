using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private bool _isModified;
		private int _selectedTab;

		public SettingsPageViewModel(NavigationStateViewModel navigationState, Global global) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Global = global;
			Title = "Settings";

			_selectedTab = 0;

			// TODO: Restart wasabi message
			// IsModified = !Global.Config.AreDeepEqual(config);

			this.WhenNavigatedTo(() =>
			{
				var config = new Config(global.Config.FilePath);
				config.LoadOrCreateDefaultFile();

				GeneralTab = new GeneralTabViewModel(Global, config);
				PrivacyTab = new PrivacyTabViewModel(Global, config);
				NetworkTab = new NetworkTabViewModel(Global, config);
				BitcoinTab = new BitcoinTabViewModel(Global, config);

				return Disposable.Empty;
			});
		}

		public Global Global { get; }

		public GeneralTabViewModel? GeneralTab { get; set; }
		public PrivacyTabViewModel? PrivacyTab { get; set; }
		public NetworkTabViewModel? NetworkTab { get; set; }
		public BitcoinTabViewModel? BitcoinTab { get; set; }

		public bool IsModified
		{
			get => _isModified;
			set => this.RaiseAndSetIfChanged(ref _isModified, value);
		}

		public int SelectedTab
		{
			get => _selectedTab;
			set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
		}

		public override string IconName => "settings_regular";
	}
}