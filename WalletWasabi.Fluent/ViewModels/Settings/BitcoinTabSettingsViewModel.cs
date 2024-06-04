using System.Collections.Generic;
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
	Keywords = new[]
	{
			"Settings", "Bitcoin", "Network", "Main", "TestNet", "RegTest", "Run", "Node", "Core", "Knots", "Version", "Startup",
			"P2P", "Endpoint", "Dust", "Threshold", "BTC"
	},
	IconName = "settings_bitcoin_regular")]
public partial class BitcoinTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _bitcoinP2PEndPoint;
	[AutoNotify] private string _coordinatorUri;
	[AutoNotify] private string _dustThreshold;

	[AutoNotify] private bool _focusCoordinatorUri;

	public BitcoinTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);
		this.ValidateProperty(x => x.CoordinatorUri, ValidateCoordinatorUri);
		this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);

		_bitcoinP2PEndPoint = settings.BitcoinP2PEndPoint;
		_coordinatorUri = settings.CoordinatorUri;
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
