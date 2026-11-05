using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Connections",
	Caption = "Manage connections settings",
	Order = 3,
	Category = "Settings",
	Keywords = new[]
	{
			"Settings", "Connections", "URI", "Exchange", "Rate", "Provider", "Fiat", "Currency", "Fee", "Estimation", "Network", "Anonymization",
			"Tor", "Terminate", "Wasabi", "Shut", "Reset"
	},
	IconName = "settings_general_regular")]
public partial class ConnectionsSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private string[] _exchangeRateProviders = [];

	public ConnectionsSettingsTabViewModel(UiContext uiContext, ApplicationSettings settings) : base(uiContext)
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

		this.WhenAnyValue(x => x.Settings.FiatCurrency)
			.Select(Client.Wallets.Exchange.ExchangeRateProviders.ProviderNamesFor)
			.Subscribe(providers =>
			{
				ExchangeRateProviders = providers;
				if (!providers.Contains(Settings.ExchangeRateProvider))
				{
					Settings.ExchangeRateProvider = providers[0];
				}
			});
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public ApplicationSettings Settings { get; }

	public IEnumerable<string> FiatCurrencies => Client.Wallets.Exchange.ExchangeRateProviders.Currencies;
	public IEnumerable<string> FeeRateEstimationProviders => FeeRateProviders.Providers;
	public IEnumerable<string> ExternalBroadcastProviders { get; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();
}
