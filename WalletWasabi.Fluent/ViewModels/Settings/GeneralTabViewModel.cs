using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class GeneralTabViewModel : SettingsViewModelBase
	{
		private bool _darkModeEnabled;
		private bool _autocopy;
		private bool _customFee;
		private bool _customChangeAddress;
		private FeeDisplayFormat _selectedFeeDisplayFormat;
		private string _dustThreshold;

		public GeneralTabViewModel(Global global, Config config) : base(global)
		{
			this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);

			_darkModeEnabled = true; // TODO: Get from config file.
			_autocopy = global.UiConfig.Autocopy;
			_customFee = global.UiConfig.IsCustomFee;
			_customChangeAddress = global.UiConfig.IsCustomChangeAddress;
			_selectedFeeDisplayFormat = Enum.IsDefined(typeof(FeeDisplayFormat), global.UiConfig.FeeDisplayFormat)
				? (FeeDisplayFormat)global.UiConfig.FeeDisplayFormat
				: FeeDisplayFormat.SatoshiPerByte;
			_dustThreshold = config.DustThreshold.ToString();

			this.WhenAnyValue(x => x.DustThreshold)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(_ => Save());

			this.WhenAnyValue(x => x.DarkModeEnabled)
				.Skip(1)
				.Subscribe(
					_ =>
					{
						var currentTheme = Application.Current.Styles.Select(x => (StyleInclude)x).FirstOrDefault(x => x.Source is { } && x.Source.AbsolutePath.Contains("Themes"));

						if (currentTheme?.Source is { } src)
						{
							var themeIndex = Application.Current.Styles.IndexOf(currentTheme);

							var newTheme = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.xaml"))
							{
								Source = new Uri($"avares://WalletWasabi.Fluent/Styles/Themes/{(src.AbsolutePath.Contains("Light") ? "BaseDark" : "BaseLight")}.xaml")
							};

							Application.Current.Styles[themeIndex] = newTheme;
						}
					});

			this.WhenAnyValue(x => x.Autocopy)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => global.UiConfig.Autocopy = x);

			this.WhenAnyValue(x => x.CustomFee)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => global.UiConfig.IsCustomFee = x);

			this.WhenAnyValue(x => x.CustomChangeAddress)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => global.UiConfig.IsCustomChangeAddress = x);

			this.WhenAnyValue(x => x.SelectedFeeDisplayFormat)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => global.UiConfig.FeeDisplayFormat = (int)x);
		}

		public bool DarkModeEnabled
		{
			get => _darkModeEnabled;
			set => this.RaiseAndSetIfChanged(ref _darkModeEnabled, value);
		}

		public bool Autocopy
		{
			get => _autocopy;
			set => this.RaiseAndSetIfChanged(ref _autocopy, value);
		}

		public bool CustomFee
		{
			get => _customFee;
			set => this.RaiseAndSetIfChanged(ref _customFee, value);
		}

		public bool CustomChangeAddress
		{
			get => _customChangeAddress;
			set => this.RaiseAndSetIfChanged(ref _customChangeAddress, value);
		}

		public FeeDisplayFormat SelectedFeeDisplayFormat
		{
			get => _selectedFeeDisplayFormat;
			set => this.RaiseAndSetIfChanged(ref _selectedFeeDisplayFormat, value);
		}

		public string DustThreshold
		{
			get => _dustThreshold;
			set => this.RaiseAndSetIfChanged(ref _dustThreshold, value);
		}

		public IEnumerable<FeeDisplayFormat> FeeDisplayFormats => Enum.GetValues(typeof(FeeDisplayFormat)).Cast<FeeDisplayFormat>();

		private void ValidateDustThreshold(IValidationErrors errors) => ValidateDustThreshold(errors, DustThreshold, whiteSpaceOk: true);

		private void ValidateDustThreshold(IValidationErrors errors, string dustThreshold, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(dustThreshold))
			{
				if (!string.IsNullOrEmpty(dustThreshold) && dustThreshold.Contains(',', StringComparison.InvariantCultureIgnoreCase))
				{
					errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
				}

				if (!decimal.TryParse(dustThreshold, out var dust) || dust < 0)
				{
					errors.Add(ErrorSeverity.Error, "Invalid dust threshold.");
				}
			}
		}

		protected override void EditConfigOnSave(Config config)
		{
			config.DustThreshold = decimal.TryParse(DustThreshold, out var threshold) ? Money.Coins(threshold) : Config.DefaultDustThreshold;
		}
	}
}