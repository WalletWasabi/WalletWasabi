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
	/// <summary>
	/// Tests <see cref="PersistentConfigManager.ToFile{T}(string, T)"/> and <see cref="PersistentConfigManager.LoadFile{TResponse}(string, bool)"/>.
	/// </summary>
	[Fact]
	public async Task ToFileAndLoadFileTestAsync()
	{
		string workDirectory = await Common.GetEmptyWorkDirAsync();
		string configPath = Path.Combine(workDirectory, $"{nameof(ToFileAndLoadFileTestAsync)}.json");

		string expectedLocalBitcoinCoreDataDir = nameof(PersistentConfigManagerTests);

		// Create config and store it.
		PersistentConfig actualConfig = new();

		string storedJson = PersistentConfigManager.ToFile(configPath, actualConfig);
		PersistentConfig readConfig = PersistentConfigManager.LoadFile(configPath);

		// Objects are supposed to be equal by value-equality rules.
		Assert.True(actualConfig.DeepEquals(readConfig));

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
			  "MainNetBackendUri": "https://api.wasabiwallet.io/",
			  "TestNetBackendUri": "https://api.wasabiwallet.co/",
			  "RegTestBackendUri": "http://localhost:37127/",
			  "MainNetCoordinatorUri": "",
			  "TestNetCoordinatorUri": "",
			  "RegTestCoordinatorUri": "http://localhost:37128/",
			  "UseTor": "Enabled",
			  "TerminateTorOnExit": false,
			  "TorBridges": [],
			  "DownloadNewVersion": true,
			  "UseBitcoinRpc": false,
			  "BitcoinRpcCredentialString": "",
			  "MainNetBitcoinRpcEndPoint": "127.0.0.1:8332",
			  "TestNetBitcoinRpcEndPoint": "127.0.0.1:48332",
			  "RegTestBitcoinRpcEndPoint": "127.0.0.1:18443",
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
			  "MaxCoinJoinMiningFeeRate": 150.0,
			  "AbsoluteMinInputCount": 21,
			  "ConfigVersion": 0
			}
			""";

		static void AssertJsonStringsEqual(string expected, string actual)
			=> Assert.Equal(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
	}
}
