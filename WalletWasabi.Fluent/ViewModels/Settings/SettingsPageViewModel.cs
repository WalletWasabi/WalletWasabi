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
				GeneralTab = new GeneralTabTabViewModel(global);
				PrivacyTab = new PrivacyTabTabViewModel(global);
				NetworkTab = new NetworkTabTabViewModel(global);
				BitcoinTab = new BitcoinTabTabViewModel(global);

				SettingsTabViewModelBase.RestartNeeded += OnRestartNeeded;

				return Disposable.Create(() =>
				{
					GeneralTab = null;
					PrivacyTab = null;
					NetworkTab = null;
					BitcoinTab = null;

					SettingsTabViewModelBase.RestartNeeded -= OnRestartNeeded;
				});
			});
		}

		public GeneralTabTabViewModel? GeneralTab { get; set; }
		public PrivacyTabTabViewModel? PrivacyTab { get; set; }
		public NetworkTabTabViewModel? NetworkTab { get; set; }
		public BitcoinTabTabViewModel? BitcoinTab { get; set; }

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

		private void OnRestartNeeded(object? sender, RestartNeededEventArgs e)
		{
			IsModified = e.IsRestartNeeded;
		}
	}
}
