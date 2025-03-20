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
	Keywords =
	[
		"Settings", "Bitcoin", "Network", "Main", "TestNet", "TestNet4", "RegTest", "Run", "Node", "Core", "Knots", "Version", "Startup",
		"Stop", "Shutdown", "Rpc", "Endpoint", "Dust", "Attack", "Limit"
	],
	IconName = "settings_bitcoin_regular")]
public partial class BitcoinTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _bitcoinRpcEndPoint;
	[AutoNotify] private string _dustThreshold;

	public BitcoinTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.BitcoinRpcEndPoint, ValidateBitcoinRpcEndPoint);
		this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);

		_bitcoinRpcEndPoint = settings.BitcoinRpcEndPoint;
		_dustThreshold = settings.DustThreshold;

		this.WhenAnyValue(x => x.Settings.BitcoinRpcEndPoint)
			.Subscribe(x => BitcoinRpcEndPoint = x);

		this.WhenAnyValue(x => x.Settings.DustThreshold)
			.Subscribe(x => DustThreshold = x);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

	public IEnumerable<Network> Networks { get; } = new[] { Network.Main, Network.TestNet, Network.RegTest };

	private void ValidateBitcoinRpcEndPoint(IValidationErrors errors)
	{
		if (!string.IsNullOrWhiteSpace(BitcoinRpcEndPoint))
		{
			if (!EndPointParser.TryParse(BitcoinRpcEndPoint, Settings.Network.DefaultPort, out _))
			{
				errors.Add(ErrorSeverity.Error, "Invalid endpoint.");
			}
			else
			{
				Settings.BitcoinRpcEndPoint = BitcoinRpcEndPoint;
			}
		}
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
				errors.Add(ErrorSeverity.Error, "Invalid dust attack limit.");
			}

			if (!error)
			{
				Settings.DustThreshold = dustThreshold;
			}
		}
	}
}
