using System;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class PrivacyTabTabViewModel : SettingsTabViewModelBase
	{
		private int _minimalPrivacyLevel;
		private int _mediumPrivacyLevel;
		private int _strongPrivacyLevel;

		public PrivacyTabTabViewModel(Global global) : base(global)
		{
			_minimalPrivacyLevel = global.Config.PrivacyLevelSome;
			_mediumPrivacyLevel = global.Config.PrivacyLevelFine;
			_strongPrivacyLevel = global.Config.PrivacyLevelStrong;

			this.WhenAnyValue(
					x => x.MinimalPrivacyLevel,
					x => x.MediumPrivacyLevel,
					x => x.StrongPrivacyLevel)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Throttle(TimeSpan.FromSeconds(1))
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

		public int MinimalPrivacyLevel
		{
			get => _minimalPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _minimalPrivacyLevel, value);
		}

		public int MediumPrivacyLevel
		{
			get => _mediumPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _mediumPrivacyLevel, value);
		}

		public int StrongPrivacyLevel
		{
			get => _strongPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _strongPrivacyLevel, value);
		}

		protected override void EditConfigOnSave(Config config)
		{
			config.PrivacyLevelSome = MinimalPrivacyLevel;
			config.PrivacyLevelFine = MediumPrivacyLevel;
			config.PrivacyLevelStrong = StrongPrivacyLevel;
		}
	}
}