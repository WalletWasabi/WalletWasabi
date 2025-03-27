using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
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
			"Settings", "Connections", "Backend", "URI", "Exchange", "Rate", "Provider", "Fee", "Estimation", "Network", "Anonymization",
			"Tor", "Terminate", "Wasabi", "Shut", "Reset"
	},
	IconName = "settings_general_regular")]
public partial class ConnectionsSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private string _backendUri;

	public ConnectionsSettingsTabViewModel(IApplicationSettings settings)
	{
		Settings = settings;
		_backendUri = settings.BackendUri;

		if (settings.Network == Network.Main)
		{
			ExternalBroadcastProviders = ExternalTransactionBroadcaster.Providers.Select(x => x.Name);
		}
		else
		{
			ExternalBroadcastProviders = ExternalTransactionBroadcaster.TestNet4Providers.Select(x => x.Name);
		}

		this.ValidateProperty(x => x.BackendUri, ValidateBackendUri);

		this.WhenAnyValue(x => x.Settings.BackendUri)
			.Subscribe(x => BackendUri = x);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public IEnumerable<string> ExchangeRateProviders => ExchangeRateProvider.Providers.Select(x => x.Name);
	public IEnumerable<string> FeeRateEstimationProviders => FeeRateProvider.Providers.Select(x => x.Name);
	public IEnumerable<string> ExternalBroadcastProviders { get; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();

	private void ValidateBackendUri(IValidationErrors errors)
	{
		var backendUri = BackendUri;

		if (string.IsNullOrEmpty(backendUri))
		{
			return;
		}

		if (!Uri.TryCreate(backendUri, UriKind.Absolute, out _))
		{
			errors.Add(ErrorSeverity.Error, "Invalid URI.");
			return;
		}

		Settings.BackendUri = backendUri;
	}
}
