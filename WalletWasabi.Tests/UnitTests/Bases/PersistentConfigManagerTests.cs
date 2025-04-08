using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Daemon;
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
		PersistentConfig actualConfig = new();

		string storedJson = PersistentConfigManager.ToFile(configPath, actualConfig);
		var readConfig = PersistentConfigManager.LoadFile(configPath) as PersistentConfig;

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
			  "Network": "Main",
			  "BackendUri": "https://api.wasabiwallet.io/",
			  "CoordinatorUri": "",
			  "UseTor": "Enabled",
			  "TerminateTorOnExit": false,
			  "TorBridges": [],
			  "DownloadNewVersion": true,
			  "UseBitcoinRpc": false,
			  "BitcoinRpcCredentialString": "",
			  "BitcoinRpcEndPoint": "127.0.0.1:8332",
			  "JsonRpcServerEnabled": false,
			  "JsonRpcUser": "",
			  "JsonRpcPassword": "",
			  "JsonRpcServerPrefixes": [
			    "http://127.0.0.1:37128/",
			    "http://localhost:37128/"
			  ],
			  "DustThreshold": "0.00005",
			  "EnableGpu": true,
			  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
			  "ExchangeRateProvider": "MempoolSpace",
			  "FeeRateEstimationProvider": "MempoolSpace",
			  "ExternalTransactionBroadcaster": "MempoolSpace",
			  "MaxCoinJoinMiningFeeRate": 150.0,
			  "AbsoluteMinInputCount": 21,
			  "ConfigVersion": 0
			}
			""";

		static void AssertJsonStringsEqual(string expected, string actual)
			=> Assert.Equal(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
	}
}
