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
			"Settings", "Connections", "Indexer", "URI", "Exchange", "Rate", "Provider", "Fee", "Estimation", "Network", "Anonymization",
			"Tor", "Terminate", "Wasabi", "Shut", "Reset"
	},
	IconName = "settings_general_regular")]
public partial class ConnectionsSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private string _indexerUri;

	public ConnectionsSettingsTabViewModel(IApplicationSettings settings)
	{
		Settings = settings;
		_indexerUri = settings.IndexerUri;

		if (settings.Network == Network.Main)
		{
			ExternalBroadcastProviders = ExternalTransactionBroadcaster.Providers.Select(x => x.Name);
		}
		else
		{
			ExternalBroadcastProviders = ExternalTransactionBroadcaster.TestNet4Providers.Select(x => x.Name);
		}

		this.ValidateProperty(x => x.IndexerUri, ValidateIndexerUri);

		this.WhenAnyValue(x => x.Settings.IndexerUri)
			.Subscribe(x => IndexerUri = x);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public IEnumerable<string> ExchangeRateProviders => ExchangeRateProvider.Providers.Select(x => x.Name);
	public IEnumerable<string> FeeRateEstimationProviders => FeeRateProviders.Providers;
	public IEnumerable<string> ExternalBroadcastProviders { get; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();

	private void ValidateIndexerUri(IValidationErrors errors)
	{
		var indexerUri = IndexerUri;

		if (string.IsNullOrEmpty(indexerUri))
		{
			return;
		}

		if (!Uri.TryCreate(indexerUri, UriKind.Absolute, out _))
		{
			errors.Add(ErrorSeverity.Error, "Invalid URI.");
			return;
		}

		Settings.IndexerUri = indexerUri;
	}
}
