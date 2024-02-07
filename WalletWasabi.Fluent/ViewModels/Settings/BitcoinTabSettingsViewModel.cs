using System.Collections.Generic;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings;

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

	public BitcoinTabSettingsViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		Settings = uiContext.ApplicationSettings;

		this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);

		_bitcoinP2PEndPoint = Settings.BitcoinP2PEndPoint;

		var dustThreshold = CurrencyInput.TryParse(Settings.DustThreshold);

		DustThreshold = new CurrencyInputViewModel(uiContext, CurrencyFormat.Btc, null);

		DustThreshold.SetValue(dustThreshold);

		this.WhenAnyValue(x => x.DustThreshold.Value)
			.Select(x => x.ToInvariantFormatString())
			.BindTo(Settings, x => x.DustThreshold);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public CurrencyInputViewModel DustThreshold { get; }

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
}
