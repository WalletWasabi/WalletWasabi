using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using NBitcoin.RPC;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Bitcoin",
	Caption = "Manage Bitcoin settings",
	Order = 1,
	Category = "Settings",
	Keywords =
	[
		"Settings", "Bitcoin", "Network", "Main", "TestNet", "TestNet4", "Signet", "RegTest", "Run", "Node", "Core", "Knots", "Version", "Startup",
		"Stop", "Shutdown", "Rpc", "Endpoint", "Dust", "Attack", "Limit"
	],
	IconName = "settings_bitcoin_regular")]
public partial class BitcoinTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _bitcoinRpcUri;
	[AutoNotify] private string _bitcoinRpcCredentialString;
	[AutoNotify] private string _dustThreshold;
	[AutoNotify] private string? _connectionStatusMessage;
	[AutoNotify] private bool _connectionStatusIsSuccess;

	public BitcoinTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.BitcoinRpcUri, ValidateBitcoinRpcUri);
		this.ValidateProperty(x => x.BitcoinRpcCredentialString, ValidateBitcoinRpcCredentialString);
		this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);

		_bitcoinRpcUri = settings.BitcoinRpcUri;
		_bitcoinRpcCredentialString = settings.BitcoinRpcCredentialString;
		_dustThreshold = settings.DustThreshold;

		this.WhenAnyValue(x => x.Settings.BitcoinRpcUri)
			.Subscribe(x => BitcoinRpcUri = x);

		this.WhenAnyValue(x => x.Settings.BitcoinRpcCredentialString)
			.Subscribe(x => BitcoinRpcCredentialString = x);

		this.WhenAnyValue(x => x.Settings.DustThreshold)
			.Subscribe(x => DustThreshold = x);

		// Enable verify button only when URI is valid
		var canVerify = this.WhenAnyValue(
			x => x.BitcoinRpcUri,
			(uri) => !string.IsNullOrWhiteSpace(uri) && Uri.TryCreate(uri, UriKind.Absolute, out _));

		VerifyConnectionCommand = ReactiveCommand.CreateFromTask(VerifyConnectionAsync, canVerify);

		// Clear status message when inputs change
		this.WhenAnyValue(x => x.BitcoinRpcUri, x => x.BitcoinRpcCredentialString)
			.Subscribe(_ => ConnectionStatusMessage = null);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public IEnumerable<Network> Networks { get; } = new[] { Network.Main, Network.TestNet, Bitcoin.Instance.Signet, Network.RegTest };

	public ICommand VerifyConnectionCommand { get; }

	private void ValidateBitcoinRpcUri(IValidationErrors errors)
	{
		if (!string.IsNullOrWhiteSpace(BitcoinRpcUri))
		{
			if (!Uri.TryCreate(BitcoinRpcUri, UriKind.Absolute, out _))
			{
				errors.Add(ErrorSeverity.Error, "Invalid bitcoin rpc uri.");
			}
			else
			{
				Settings.BitcoinRpcUri = BitcoinRpcUri;
			}
		}
	}

	private void ValidateBitcoinRpcCredentialString(IValidationErrors errors)
	{
		if (string.IsNullOrWhiteSpace(BitcoinRpcCredentialString) || RPCCredentialString.TryParse(BitcoinRpcCredentialString, out _))
		{
			Settings.BitcoinRpcCredentialString = BitcoinRpcCredentialString;
		}
		else
		{
			errors.Add(ErrorSeverity.Error, "Invalid bitcoin rpc credential string.");
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

	private async Task VerifyConnectionAsync()
	{
		try
		{
			// Parse credentials
			RPCCredentialString credentials;
			if (string.IsNullOrWhiteSpace(BitcoinRpcCredentialString))
			{
				// Use default credentials if empty
				credentials = new RPCCredentialString();
			}
			else if (!RPCCredentialString.TryParse(BitcoinRpcCredentialString, out credentials!))
			{
				ConnectionStatusIsSuccess = false;
				ConnectionStatusMessage = "Invalid credentials format";
				return;
			}

			// Create RPC client
			var rpcClient = new RPCClient(credentials, BitcoinRpcUri, Settings.Network);

			// Test connection with GetBlockchainInfo
			await rpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);

			// Success
			ConnectionStatusIsSuccess = true;
			ConnectionStatusMessage = "Connected successfully";
		}
		catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
		{
			// Unauthorized
			ConnectionStatusIsSuccess = false;
			ConnectionStatusMessage = "Connection attempt failed (Unauthorized)";
		}
		catch (Exception)
		{
			// Connection failed
			ConnectionStatusIsSuccess = false;
			ConnectionStatusMessage = "Connection attempt failed";
		}
	}
}
