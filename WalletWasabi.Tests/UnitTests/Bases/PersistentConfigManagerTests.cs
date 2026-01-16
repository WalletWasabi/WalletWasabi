using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Daemon.Configuration;
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
			  "BackendUri": "https://api.wasabiwallet.io/",
			  "CoordinatorUri": "",
			  "UseTor": "Enabled",
			  "TerminateTorOnExit": false,
			  "TorBridges": [],
			  "DownloadNewVersion": true,
			  "UseBitcoinRpc": false,
			  "BitcoinRpcCredentialString": "",
			  "BitcoinRpcEndPoint": "http://localhost:8332",
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
			  "ConfigVersion": 2
			}
			""";

		static void AssertJsonStringsEqual(string expected, string actual)
			=> Assert.Equal(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
	}
}
