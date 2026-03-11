using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Bases;
using WalletWasabi.Daemon;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Bases;

/// <summary>
/// Tests for <see cref="PersistentConfigManager"/>
/// </summary>
public class PersistentConfigManagerTests
{
	[Fact]
	public async Task ToFileAndLoadFileTestAsync()
	{
		string workDirectory = await Common.GetEmptyWorkDirAsync();
		string configPath = Path.Combine(workDirectory, $"{nameof(ToFileAndLoadFileTestAsync)}.json");

		string expectedLocalBitcoinCoreDataDir = nameof(PersistentConfigManagerTests);

		// Create config and store it.
		PersistentConfig actualConfig = PersistentConfigManager.DefaultMainNetConfig;

		string storedJson = PersistentConfigManager.ToFile(configPath, actualConfig);
		PersistentConfigManager.UpdateNetwork(configPath, actualConfig.Network);

		var readConfig = PersistentConfigManager.LoadFile(configPath) as PersistentConfig;
		Assert.NotNull(readConfig);

		// Objects are supposed to be equal by value-equality rules.
		Assert.Equal(actualConfig, readConfig);

		// Check that JSON strings are equal as well.
		{
			string expected = GetConfigString(expectedLocalBitcoinCoreDataDir);
			string actual = JsonEncoder.ToReadableString(readConfig, PersistentConfigEncode.PersistentConfig);

			AssertJsonStringsEqual(expected, actual);
			AssertJsonStringsEqual(expected, storedJson);
		}

		static string GetConfigString(string localBitcoinCoreDataDir)
			=> $$"""
			{
			  "CoordinatorUri": "",
			  "UseTor": "Enabled",
			  "TerminateTorOnExit": false,
			  "TorBridges": [],
			  "DownloadNewVersion": true,
			  "BitcoinRpcCredentialString": "wasabi:wasabi",
			  "BitcoinRpcEndPoint": "https://rpc.wasabiwallet.io",
			  "JsonRpcServerEnabled": false,
			  "JsonRpcUser": "",
			  "JsonRpcPassword": "",
			  "JsonRpcServerPrefixes": [
			    "http://127.0.0.1:37128/",
			    "http://localhost:37128/"
			  ],
			  "DustThreshold": "0.00001",
			  "EnableGpu": true,
			  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
			  "ExchangeRateProvider": "MempoolSpace",
			  "FeeRateEstimationProvider": "MempoolSpace",
			  "ExternalTransactionBroadcaster": "MempoolSpace",
			  "MaxCoinJoinMiningFeeRate": 50.0,
			  "AbsoluteMinInputCount": 21,
			  "MaxDaysInMempool": 30,
			  "ExperimentalFeatures": [],
			  "ConfigVersion": 3
			}
			""";

		static void AssertJsonStringsEqual(string expected, string actual)
			=> Assert.Equal(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
	}

	// Test for migration 2.6.0 -> 2.8.0
	[Fact]
	public void MigrateFromV2_6_0WithUseBitcoinRpcTrue()
	{
		// Test migration when UseBitcoinRpc=true with valid RPC URI
		// The RPC credentials and URI should be preserved
		var v2_6_0Config = new PersistentConfig_2_6_0(
			Network: Network.Main,
			IndexerUri: "https://api.wasabiwallet.io/",
			CoordinatorUri: "https://wasabiwallet.io/",
			UseTor: "Enabled",
			TerminateTorOnExit: false,
			TorBridges: ValueList<string>.Empty,
			DownloadNewVersion: true,
			UseBitcoinRpc: true,
			BitcoinRpcCredentialString: "user:password",
			BitcoinRpcUri: "http://localhost:8332",
			JsonRpcServerEnabled: false,
			JsonRpcUser: "wasabi",
			JsonRpcPassword: "secret",
			JsonRpcServerPrefixes: new ValueList<string>(["http://127.0.0.1:37128/"]),
			DustThreshold: Money.Coins(0.00001m),
			EnableGpu: true,
			CoordinatorIdentifier: "CoinJoinCoordinatorIdentifier",
			ExchangeRateProvider: "MempoolSpace",
			FeeRateEstimationProvider: "BlockstreamInfo",
			ExternalTransactionBroadcaster: "MempoolSpace",
			MaxCoinJoinMiningFeeRate: 50.0m,
			AbsoluteMinInputCount: 21,
			MaxDaysInMempool: 30,
			ExperimentalFeatures: new ValueList<string>(["taproot"]),
			ConfigVersion: 2
		);

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		// Verify the migration
		Assert.Equal(3, migratedConfig.ConfigVersion); // Version should be updated to e

		// RPC credentials should be preserved when UseBitcoinRpc=true and URI is valid
		Assert.Equal("user:password", migratedConfig.BitcoinRpcCredentialString);
		Assert.Equal("http://localhost:8332", migratedConfig.BitcoinRpcUri);
	}

	private static readonly PersistentConfig_2_6_0 Template_2_6_0 = new(
		Network: Network.Main,
		IndexerUri: "https://api.wasabiwallet.io/",
		CoordinatorUri: "https://wasabiwallet.io/",
		UseTor: "Enabled",
		TerminateTorOnExit: false,
		TorBridges: ValueList<string>.Empty,
		DownloadNewVersion: true,
		UseBitcoinRpc: false,
		BitcoinRpcCredentialString: "",
		BitcoinRpcUri: "", // Empty/invalid URI
		JsonRpcServerEnabled: false,
		JsonRpcUser: "",
		JsonRpcPassword: "",
		JsonRpcServerPrefixes: new ValueList<string>(["http://127.0.0.1:37128/"]),
		DustThreshold: Money.Coins(0.00001m),
		EnableGpu: true,
		CoordinatorIdentifier: "CoinJoinCoordinatorIdentifier",
		ExchangeRateProvider: "MempoolSpace",
		FeeRateEstimationProvider: "BlockstreamInfo",
		ExternalTransactionBroadcaster: "MempoolSpace",
		MaxCoinJoinMiningFeeRate: 50.0m,
		AbsoluteMinInputCount: 21,
		MaxDaysInMempool: 30,
		ExperimentalFeatures: ValueList<string>.Empty,
		ConfigVersion: 2
	);

	[Fact]
	public void MigrateFromV2_6_0WithUseBitcoinRpcFalseAndInvalidUri()
	{
		// Test migration when UseBitcoinRpc=false AND BitcoinRpcUri is invalid/empty
		// This should trigger migration to Wasabi RPC service
		var v2_6_0Config = Template_2_6_0 with
		{
			IndexerUri = "https://api.wasabiwallet.io/",
			UseBitcoinRpc = false,
			BitcoinRpcCredentialString = "",
			BitcoinRpcUri = "", // Empty/invalid URI
		};

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		// When UseBitcoinRpc=false and URI is invalid, should migrate to Wasabi RPC
		Assert.Equal("wasabi:wasabi", migratedConfig.BitcoinRpcCredentialString);
		Assert.Equal("https://rpc.wasabiwallet.io", migratedConfig.BitcoinRpcUri);
		Assert.Equal(3, migratedConfig.ConfigVersion);
	}

	[Fact]
	public void MigrateFromV2_6_0WithUseBitcoinRpcFalseButValidUri()
	{
		// Test migration when UseBitcoinRpc=false BUT BitcoinRpcUri is valid
		// The valid URI should be preserved
		var v2_6_0Config = Template_2_6_0 with
		{
			IndexerUri = "https://api.wasabiwallet.io/",
			UseBitcoinRpc = false,
			BitcoinRpcCredentialString = "myuser:mypass",
			BitcoinRpcUri = "https://rpc.wasabiwallet.io",
		};

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		// When URI is valid, it should be preserved even if UseBitcoinRpc=false
		Assert.Equal("wasabi:wasabi", migratedConfig.BitcoinRpcCredentialString);
		Assert.Equal("https://rpc.wasabiwallet.io", migratedConfig.BitcoinRpcUri);
		Assert.Equal(3, migratedConfig.ConfigVersion);
	}

	[Fact]
	public void MigrateFromV2_6_0_IndexerUriIsDropped()
	{
		// Test that IndexerUri field is dropped during migration
		var v2_6_0Config = Template_2_6_0 with
		{
			IndexerUri = "https://api.wasabiwallet.io/",
			UseBitcoinRpc = true,
			BitcoinRpcCredentialString = "user:password",
			BitcoinRpcUri = "http://localhost:8332",
		};

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		// When URI is valid, it should be preserved even if UseBitcoinRpc=false
		Assert.Equal("user:password", migratedConfig.BitcoinRpcCredentialString);
		Assert.Equal("http://localhost:8332", migratedConfig.BitcoinRpcUri);
		Assert.Equal(3, migratedConfig.ConfigVersion);
	}

	[Fact]
	public void MigrateFromV2_6_0_AllFieldsPreserved()
	{
		// Comprehensive test that all non-removed fields are preserved during migration
		var v2_6_0Config = new PersistentConfig_2_6_0(
			Network: Network.TestNet,
			IndexerUri: "https://api.wasabiwallet.co/", // Should be dropped
			CoordinatorUri: "https://coordinator.test/",
			UseTor: "EnabledOnlyRunning",
			TerminateTorOnExit: true,
			TorBridges: new ValueList<string>(["bridge1", "bridge2"]),
			DownloadNewVersion: false,
			UseBitcoinRpc: true, // Should be dropped
			BitcoinRpcCredentialString: "cookiefile=/path/to/cookie",
			BitcoinRpcUri: "http://10.0.0.5:18332",
			JsonRpcServerEnabled: true,
			JsonRpcUser: "rpcuser",
			JsonRpcPassword: "rpcpass",
			JsonRpcServerPrefixes: new ValueList<string>(["http://0.0.0.0:37128/", "https://localhost:37129/"]),
			DustThreshold: Money.Coins(0.0001m),
			EnableGpu: false,
			CoordinatorIdentifier: "CustomCoordinator",
			ExchangeRateProvider: "Binance",
			FeeRateEstimationProvider: "Custom",
			ExternalTransactionBroadcaster: "Custom",
			MaxCoinJoinMiningFeeRate: 100.5m,
			AbsoluteMinInputCount: 50,
			MaxDaysInMempool: 60,
			ExperimentalFeatures: new ValueList<string>(["feature1", "feature2"]),
			ConfigVersion: 2
		);

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		// Verify all preserved fields
		Assert.Equal(Network.TestNet, migratedConfig.Network);
		Assert.Equal("https://coordinator.test/", migratedConfig.CoordinatorUri);
		Assert.Equal("EnabledOnlyRunning", migratedConfig.UseTor);
		Assert.True(migratedConfig.TerminateTorOnExit);
		Assert.Equal(2, migratedConfig.TorBridges.Count());
		Assert.Contains("bridge1", migratedConfig.TorBridges);
		Assert.Contains("bridge2", migratedConfig.TorBridges);
		Assert.False(migratedConfig.DownloadNewVersion);
		Assert.Equal("cookiefile=/path/to/cookie", migratedConfig.BitcoinRpcCredentialString);
		Assert.Equal("http://10.0.0.5:18332", migratedConfig.BitcoinRpcUri);
		Assert.True(migratedConfig.JsonRpcServerEnabled);
		Assert.Equal("rpcuser", migratedConfig.JsonRpcUser);
		Assert.Equal("rpcpass", migratedConfig.JsonRpcPassword);
		Assert.Equal(2, migratedConfig.JsonRpcServerPrefixes.Count());
		Assert.Equal(Money.Coins(0.0001m), migratedConfig.DustThreshold);
		Assert.False(migratedConfig.EnableGpu);
		Assert.Equal("CustomCoordinator", migratedConfig.CoordinatorIdentifier);
		Assert.Equal("Binance", migratedConfig.ExchangeRateProvider);
		Assert.Equal("Custom", migratedConfig.FeeRateEstimationProvider);
		Assert.Equal("Custom", migratedConfig.ExternalTransactionBroadcaster);
		Assert.Equal(100.5m, migratedConfig.MaxCoinJoinMiningFeeRate);
		Assert.Equal(50, migratedConfig.AbsoluteMinInputCount);
		Assert.Equal(60, migratedConfig.MaxDaysInMempool);

		// ExperimentalFeatures should be reset to empty (as per migration logic)
		Assert.Empty(migratedConfig.ExperimentalFeatures);

		// ConfigVersion should be updated
		Assert.Equal(3, migratedConfig.ConfigVersion);
	}

	[Fact]
	public void MigrateFromV2_6_0_WithMalformedUri()
	{
		// Test migration when BitcoinRpcUri contains a malformed URI (not a valid absolute URI)
		var v2_6_0Config = Template_2_6_0 with
		{
			UseBitcoinRpc = false,
			BitcoinRpcCredentialString = "",
			BitcoinRpcUri = "not-a-valid-uri", // Malformed - not an absolute URI
		};

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		// Should migrate to Wasabi RPC service because UseBitcoinRpc=false and URI is not well-formed
		Assert.Equal("wasabi:wasabi", migratedConfig.BitcoinRpcCredentialString);
		Assert.Equal("https://rpc.wasabiwallet.io", migratedConfig.BitcoinRpcUri);
	}

	[Fact]
	public void MigrateFromV2_6_0_ConfigVersionUpdated()
	{
		// Test that ConfigVersion is correctly updated from 2 to 3
		var v2_6_0Config = Template_2_6_0 with {
			ConfigVersion = 2 // v2.6.0 uses ConfigVersion 2
		};

		var migratedConfig = WasabiApplication.UpdateFrom260To280(v2_6_0Config);

		Assert.Equal(2, v2_6_0Config.ConfigVersion);
		Assert.Equal(3, migratedConfig.ConfigVersion); // Should be 4 in v2.8.0
	}
}
