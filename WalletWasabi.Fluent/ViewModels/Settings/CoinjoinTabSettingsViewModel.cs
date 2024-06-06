using System.Globalization;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Coinjoin",
	Caption = "Manage Coinjoin settings",
	Order = 2,
	Category = "Settings",
	Keywords =
	[
		"Settings", "Bitcoin", "BTC", "Coordinator", "Coordination", "Fee", "Coinjoin"
	],
	IconName = "settings_bitcoin_regular")]
public partial class CoinjoinTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _coordinatorUri;
	[AutoNotify] private string _maxCoordinationFeeRate;

	[AutoNotify] private bool _focusCoordinatorUri;

	public CoinjoinTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.CoordinatorUri, ValidateCoordinatorUri);
		this.ValidateProperty(x => x.MaxCoordinationFeeRate, ValidateMaxCoordinationFeeRate);

		_coordinatorUri = settings.CoordinatorUri;
		_maxCoordinationFeeRate = settings.MaxCoordinationFeeRate;

		this.WhenAnyValue(x => x.Settings.CoordinatorUri)
			.Subscribe(x => CoordinatorUri = x);
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
			errors.Add(ErrorSeverity.Error, "Invalid URI.");
			return;
		}

		Settings.CoordinatorUri = coordinatorUri;
	}

	private void ValidateMaxCoordinationFeeRate(IValidationErrors errors)
	{
		var maxCoordinationFeeRate = MaxCoordinationFeeRate;

		if (string.IsNullOrEmpty(maxCoordinationFeeRate))
		{
			return;
		}

		if (!decimal.TryParse(maxCoordinationFeeRate, out var maxCoordinationFeeRateDecimal))
		{
			errors.Add(ErrorSeverity.Error, "Invalid number.");
			return;
		}

		if (maxCoordinationFeeRateDecimal < 0)
		{
			errors.Add(ErrorSeverity.Error, "Cannot be lower than 0.0%");
			return;
		}

		if (maxCoordinationFeeRateDecimal > 1)
		{
			errors.Add(ErrorSeverity.Error, "Absolute maximum coordination fee rate is 1%");
			return;
		}

		Settings.MaxCoordinationFeeRate = maxCoordinationFeeRateDecimal.ToString(CultureInfo.InvariantCulture);
	}
}
