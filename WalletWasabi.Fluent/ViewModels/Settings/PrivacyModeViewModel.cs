using System;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class PrivacyModeViewModel : NavBarItemViewModel
	{
		private bool _privacyMode;

		public PrivacyModeViewModel(UiConfig uiConfig)
		{
			_privacyMode = uiConfig.PrivacyMode;

			SelectionMode = NavBarItemSelectionMode.Toggle;

			ToggleTitle();

			this.WhenAnyValue(x => x.PrivacyMode)
				.Skip(1)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(
					x =>
				{
					ToggleTitle();
					this.RaisePropertyChanged(nameof(IconName));
					uiConfig.PrivacyMode = x;
				});
		}

		public override string IconName => _privacyMode ? "privacy_mode_on" : "privacy_mode_off";

		public bool PrivacyMode
		{
			get => _privacyMode;
			set => this.RaiseAndSetIfChanged(ref _privacyMode, value);
		}

		public override void Toggle()
		{
			PrivacyMode = !PrivacyMode;
		}

		private void ToggleTitle()
		{
			Title = $"Privacy Mode {(_privacyMode ? "(On)" : "(Off)")}";
		}
	}
}