using System;
using System.Reactive;
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

			this.WhenNavigatedTo(() =>
			{
				GeneralTab = new GeneralTabViewModel(global);
				PrivacyTab = new PrivacyTabViewModel(global);
				NetworkTab = new NetworkTabViewModel(global);
				BitcoinTab = new BitcoinTabViewModel(global);

				SettingsViewModelBase.RestartNeeded += OnRestartNeeded;

				return Disposable.Create(() =>
				{
					GeneralTab = null;
					PrivacyTab = null;
					NetworkTab = null;
					BitcoinTab = null;

					SettingsViewModelBase.RestartNeeded -= OnRestartNeeded;
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

		private void OnRestartNeeded(object? sender, RestartNeedEventArgs e)
		{
			IsModified = e.IsRestartNeeded;
		}
	}
}
