using System;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	[NavigationMetaData(
		Title = "Privacy",
		Caption = "Manage privacy settings",
		Order = 1,
		Category = "Settings",
		Keywords = new[] { "Settings", "Privacy", "Minimal", "Medium", "Strong", "Anonymity", "Level" },
		IconName = "settings_privacy_regular")]
	public partial class PrivacySettingsTabViewModel : SettingsTabViewModelBase
	{
		[AutoNotify] private int _minimalPrivacyLevel;
		[AutoNotify] private int _mediumPrivacyLevel;
		[AutoNotify] private int _strongPrivacyLevel;

		public PrivacySettingsTabViewModel()
		{
			_minimalPrivacyLevel = Services.Config.PrivacyLevelSome;
			_mediumPrivacyLevel = Services.Config.PrivacyLevelFine;
			_strongPrivacyLevel = Services.Config.PrivacyLevelStrong;

			this.WhenAnyValue(
					x => x.MinimalPrivacyLevel,
					x => x.MediumPrivacyLevel,
					x => x.StrongPrivacyLevel)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
				.Skip(1)
				.Subscribe(_ => Save());

			this.WhenAnyValue(x => x.MinimalPrivacyLevel)
				.Subscribe(
					x =>
					{
						if (x >= MediumPrivacyLevel)
						{
							MediumPrivacyLevel = x + 1;
						}
					});

			this.WhenAnyValue(x => x.MediumPrivacyLevel)
				.Subscribe(
					x =>
					{
						if (x >= StrongPrivacyLevel)
						{
							StrongPrivacyLevel = x + 1;
						}

						if (x <= MinimalPrivacyLevel)
						{
							MinimalPrivacyLevel = x - 1;
						}
					});

			this.WhenAnyValue(x => x.StrongPrivacyLevel)
				.Subscribe(
					x =>
					{
						if (x <= MinimalPrivacyLevel)
						{
							MinimalPrivacyLevel = x - 1;
						}

						if (x <= MediumPrivacyLevel)
						{
							MediumPrivacyLevel = x - 1;
						}
					});
		}

		protected override void EditConfigOnSave(Config config)
		{
			config.PrivacyLevelSome = MinimalPrivacyLevel;
			config.PrivacyLevelFine = MediumPrivacyLevel;
			config.PrivacyLevelStrong = StrongPrivacyLevel;
		}
	}
}
