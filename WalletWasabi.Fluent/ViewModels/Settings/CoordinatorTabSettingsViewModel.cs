using System.Globalization;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.Settings,
	Title = "CoordinatorTabSettingsViewModel_Title",
	Caption = "CoordinatorTabSettingsViewModel_Caption",
	Keywords = "CoordinatorTabSettingsViewModel_Keywords",
	IconName = "settings_bitcoin_regular")]
public partial class CoordinatorTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _coordinatorUri;
	[AutoNotify] private string _maxCoinJoinMiningFeeRate;
	[AutoNotify] private string _absoluteMinInputCount;

	public CoordinatorTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.CoordinatorUri, ValidateCoordinatorUri);
		this.ValidateProperty(x => x.MaxCoinJoinMiningFeeRate, ValidateMaxCoinJoinMiningFeeRate);
		this.ValidateProperty(x => x.AbsoluteMinInputCount, ValidateAbsoluteMinInputCount);


		_coordinatorUri = settings.GetCoordinatorUri();
		_maxCoinJoinMiningFeeRate = settings.MaxCoinJoinMiningFeeRate;
		_absoluteMinInputCount = settings.AbsoluteMinInputCount;

		this.WhenAnyValue(
				x => x.Settings.MainNetCoordinatorUri,
				x => x.Settings.TestNetCoordinatorUri,
				x => x.Settings.RegTestCoordinatorUri,
				x => x.Settings.Network)
			.ToSignal()
			.Subscribe(x => CoordinatorUri = Settings.GetCoordinatorUri());
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	private void ValidateCoordinatorUri(IValidationErrors errors)
	{
		var coordinatorUri = CoordinatorUri;

		if (string.IsNullOrEmpty(coordinatorUri))
		{
			return;
		}

		if (!Uri.TryCreate(coordinatorUri, UriKind.Absolute, out _))
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.Sentences_InvalidURI);
			return;
		}

		Settings.TrySetCoordinatorUri(coordinatorUri);
	}

	private void ValidateMaxCoinJoinMiningFeeRate(IValidationErrors errors)
	{
		var maxCoinJoinMiningFeeRate = MaxCoinJoinMiningFeeRate;

		if (string.IsNullOrEmpty(maxCoinJoinMiningFeeRate))
		{
			return;
		}

		if (!decimal.TryParse(maxCoinJoinMiningFeeRate, out var maxCoinJoinMiningFeeRateDecimal))
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.Sentences_Invalid_number);
			return;
		}

		if (maxCoinJoinMiningFeeRateDecimal < 1)
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.CoordinatorTabSettingsViewModel_Error_MiningFeeRateInvalid);
			return;
		}

		Settings.MaxCoinJoinMiningFeeRate = maxCoinJoinMiningFeeRateDecimal.ToString(CultureInfo.InvariantCulture);
	}

	private void ValidateAbsoluteMinInputCount(IValidationErrors errors)
	{
		var absoluteMinInputCount = AbsoluteMinInputCount;

		if (string.IsNullOrEmpty(absoluteMinInputCount))
		{
			return;
		}

		if (!int.TryParse(absoluteMinInputCount, out var absoluteMinInputCountInt))
		{
			errors.Add(ErrorSeverity.Error, Lang.Resources.Sentences_Invalid_number);
			return;
		}

		if (absoluteMinInputCountInt < Constants.AbsoluteMinInputCount)
		{
			errors.Add(ErrorSeverity.Error, string.Format(Lang.Resources.Culture, Lang.Resources.CoordinatorTabSettingsViewModel_Error_MiningFeeRateInvalid, Constants.AbsoluteMinInputCount));
			return;
		}

		Settings.AbsoluteMinInputCount = absoluteMinInputCountInt.ToString(CultureInfo.InvariantCulture);
	}
}
