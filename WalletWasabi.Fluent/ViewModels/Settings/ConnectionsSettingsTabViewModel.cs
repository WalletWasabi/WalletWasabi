using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Connections",
	Caption = "Manage connections settings",
	Order = 3,
	Category = "Settings",
	Keywords = new[]
	{
			"Settings", "Connections", "URI", "Exchange", "Rate", "Provider", "Fee", "Estimation", "Network", "Anonymization",
			"Tor", "Terminate", "Wasabi", "Shut", "Reset"
	},
	IconName = "settings_general_regular")]
public partial class ConnectionsSettingsTabViewModel : RoutableViewModel
{
	public ConnectionsSettingsTabViewModel(ApplicationSettings settings)
	{
		Settings = settings;

		if (settings.Network == Network.Main)
		{
			ExternalBroadcastProviders = ExternalTransactionBroadcaster.Providers.Select(x => x.Name);
		}
		else
		{
			ExternalBroadcastProviders = ExternalTransactionBroadcaster.TestNet4Providers.Select(x => x.Name);
		}
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public ApplicationSettings Settings { get; }

	public IEnumerable<string> ExchangeRateProviders => WalletWasabi.Wallets.Exchange.ExchangeRateProviders.Providers;
	public IEnumerable<string> FeeRateEstimationProviders => FeeRateProviders.Providers;
	public IEnumerable<string> ExternalBroadcastProviders { get; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();
}
