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
		PersistentConfig actualConfig = new() { LocalBitcoinCoreDataDir = expectedLocalBitcoinCoreDataDir };

		string storedJson = PersistentConfigManager.ToFile(configPath, actualConfig);
		PersistentConfig readConfig = PersistentConfigManager.LoadFile(configPath);

		// Is the content of each config the same?
		Assert.Equal(expectedLocalBitcoinCoreDataDir, readConfig.LocalBitcoinCoreDataDir);

		// Objects are supposed to be equal by value-equality rules.
		Assert.True(actualConfig.DeepEquals(readConfig));

		// Check that JSON strings are equal as well.
		{
			string expected = GetConfigString(expectedLocalBitcoinCoreDataDir);
			string actual = PersistentConfigEncode.PersistentConfig(readConfig).ToJsonString(new JsonSerializerOptions{ WriteIndented = true });

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
			  "StartLocalBitcoinCoreOnStartup": false,
			  "StopLocalBitcoinCoreOnShutdown": true,
			  "LocalBitcoinCoreDataDir": "{{localBitcoinCoreDataDir}}",
			  "MainNetBitcoinP2pEndPoint": "127.0.0.1:8333",
			  "TestNetBitcoinP2pEndPoint": "127.0.0.1:48333",
			  "RegTestBitcoinP2pEndPoint": "127.0.0.1:18444",
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
			  "MaxCoinJoinMiningFeeRate": 150.0,
			  "AbsoluteMinInputCount": 21,
			  "ConfigVersion": 0
			}
			""";

		static void AssertJsonStringsEqual(string expected, string actual)
			=> Assert.Equal(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
	}
}
