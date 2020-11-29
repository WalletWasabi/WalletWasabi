using System.Reactive.Disposables;
using ReactiveUI;
using Splat;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private bool _isModified;
		private int _selectedTab;

		public SettingsPageViewModel()
		{
			var global = Locator.Current.GetService<Global>();

			Title = "Settings";

			_selectedTab = 0;

			GeneralTab = new GeneralTabTabViewModel(global);
			PrivacyTab = new PrivacyTabTabViewModel(global);
			NetworkTab = new NetworkTabTabViewModel(global);
			BitcoinTab = new BitcoinTabTabViewModel(global);
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			SettingsTabViewModelBase.RestartNeeded += OnRestartNeeded;

			disposable.Add(Disposable.Create(() =>
			{
				SettingsTabViewModelBase.RestartNeeded -= OnRestartNeeded;
			}));
		}

		public GeneralTabTabViewModel GeneralTab { get; }
		public PrivacyTabTabViewModel PrivacyTab { get; }
		public NetworkTabTabViewModel NetworkTab { get; }
		public BitcoinTabTabViewModel BitcoinTab { get; }

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
