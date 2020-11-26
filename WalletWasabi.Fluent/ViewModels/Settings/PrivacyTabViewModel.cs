using System;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class PrivacyTabViewModel : SettingsViewModelBase
	{
		private int _minimalPrivacyLevel;
		private int _mediumPrivacyLevel;
		private int _strongPrivacyLevel;

		public PrivacyTabViewModel(Global global, Config config) : base(global)
		{
			_minimalPrivacyLevel = config.PrivacyLevelSome;
			_mediumPrivacyLevel = config.PrivacyLevelFine;
			_strongPrivacyLevel = config.PrivacyLevelStrong;

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