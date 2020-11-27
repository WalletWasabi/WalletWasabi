using System;
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
			Title = "Settings";

			_selectedTab = 0;

			// TODO: Restart wasabi message
			// IsModified = !Global.Config.AreDeepEqual(config);

			this.WhenNavigatedTo(() =>
			{
				// TODO: Is it possible to Global.Config is not up to date?
				// var config = new Config(global.Config.FilePath);
				// config.LoadOrCreateDefaultFile();

				GeneralTab = new GeneralTabViewModel(global);
				PrivacyTab = new PrivacyTabViewModel(global);
				NetworkTab = new NetworkTabViewModel(global);
				BitcoinTab = new BitcoinTabViewModel(global);

				return Disposable.Create(() =>
				{
					GeneralTab = null;
					PrivacyTab = null;
					NetworkTab = null;
					BitcoinTab = null;
				});
			});
		}

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