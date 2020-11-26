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

			var config = new Config(global.Config.FilePath);
			config.LoadOrCreateDefaultFile();

			GeneralTab = new GeneralTabViewModel(Global, config);
			PrivacyTab = new PrivacyTabViewModel(Global, config);
			NetworkTab = new NetworkTabViewModel(Global, config);
			BitcoinTab = new BitcoinTabViewModel(Global, config);

			// TODO: Restart wasabi message
			IsModified = !Global.Config.AreDeepEqual(config);
		}

		public Global Global { get; }

		public GeneralTabViewModel GeneralTab { get; }
		public PrivacyTabViewModel PrivacyTab { get; }
		public NetworkTabViewModel NetworkTab { get; }
		public BitcoinTabViewModel BitcoinTab { get; }

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