using System.Collections.Generic;
using System.Globalization;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Bitcoin",
	Caption = "Manage Bitcoin settings",
	Order = 1,
	Category = "Settings",
	Keywords =
	[
		"Settings", "Bitcoin", "Network", "Main", "TestNet", "RegTest", "Run", "Node", "Core", "Knots", "Version", "Startup",
			"P2P", "Endpoint", "Dust", "Threshold", "BTC", "Coordinator", "Coordination", "Fee", "Rate", "Mining"
	],
	IconName = "settings_bitcoin_regular")]
public partial class BitcoinTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _bitcoinP2PEndPoint;
	[AutoNotify] private string _coordinatorUri;
	[AutoNotify] private string _maxCoordinationFeeRate;
	[AutoNotify] private string _maxCoinjoinMiningFeeRate;
	[AutoNotify] private string _dustThreshold;

	[AutoNotify] private bool _focusCoordinatorUri;

	public BitcoinTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);
		this.ValidateProperty(x => x.CoordinatorUri, ValidateCoordinatorUri);
		this.ValidateProperty(x => x.MaxCoordinationFeeRate, ValidateMaxCoordinationFeeRate);
		this.ValidateProperty(x => x.MaxCoinjoinMiningFeeRate, ValidateMaxCoinJoinMiningFeeRate);
		this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);

		_bitcoinP2PEndPoint = settings.BitcoinP2PEndPoint;
		_coordinatorUri = settings.CoordinatorUri;
		_maxCoordinationFeeRate = settings.MaxCoordinationFeeRate;
		_maxCoinjoinMiningFeeRate = settings.MaxCoinJoinMiningFeeRate;
		_dustThreshold = settings.DustThreshold;

		this.WhenAnyValue(x => x.Settings.BitcoinP2PEndPoint)
			.Subscribe(x => BitcoinP2PEndPoint = x);

		this.WhenAnyValue(x => x.Settings.CoordinatorUri)
			.Subscribe(x => CoordinatorUri = x);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

	public IEnumerable<Network> Networks { get; } = new[] { Network.Main, Network.TestNet, Network.RegTest };

	private void ValidateBitcoinP2PEndPoint(IValidationErrors errors)
	{
		if (!string.IsNullOrWhiteSpace(BitcoinP2PEndPoint))
		{
			if (!EndPointParser.TryParse(BitcoinP2PEndPoint, Settings.Network.DefaultPort, out _))
			{
				errors.Add(ErrorSeverity.Error, "Invalid endpoint.");
			}
			else
			{
				Settings.BitcoinP2PEndPoint = BitcoinP2PEndPoint;
			}
		}
	}

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

	private void ValidateMaxCoinJoinMiningFeeRate(IValidationErrors errors)
	{
		var maxCoinjoinMiningFeeRate = MaxCoinjoinMiningFeeRate;

		if (string.IsNullOrEmpty(maxCoinjoinMiningFeeRate))
		{
			return;
		}

		if (!decimal.TryParse(maxCoinjoinMiningFeeRate, out var maxCoinjoinMiningFeeRateDecimal))
		{
			errors.Add(ErrorSeverity.Error, "Invalid number.");
			return;
		}

		if (maxCoinjoinMiningFeeRateDecimal < 1)
		{
			errors.Add(ErrorSeverity.Error, "Mining fee rate must be at least 1 sat/vb");
			return;
		}

		if (maxCoinjoinMiningFeeRateDecimal > Constants.DefaultMaxCoinJoinMiningFeeRate.SatoshiPerByte)
		{
			errors.Add(ErrorSeverity.Error, $"Absolute maximum mining fee rate is {Constants.DefaultMaxCoinJoinMiningFeeRate.SatoshiPerByte}s/vb");
			return;
		}
		Settings.MaxCoinJoinMiningFeeRate = maxCoinjoinMiningFeeRateDecimal.ToString(CultureInfo.InvariantCulture);
	}

	private void ValidateDustThreshold(IValidationErrors errors)
	{
		var dustThreshold = DustThreshold;
		if (!string.IsNullOrWhiteSpace(dustThreshold))
		{
			bool error = false;

			if (!string.IsNullOrEmpty(dustThreshold) && dustThreshold.Contains(
				',',
				StringComparison.InvariantCultureIgnoreCase))
			{
				error = true;
				errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
			}

			if (!decimal.TryParse(dustThreshold, out var dust) || dust < 0)
			{
				error = true;
				errors.Add(ErrorSeverity.Error, "Invalid dust threshold.");
			}

			if (!error)
			{
				Settings.DustThreshold = dustThreshold;
			}
		}
	}
}
