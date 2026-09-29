using System;
using System.IO;
using System.Text;
using NBitcoin;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.Client.Configuration;

public static class PersistentConfigManager
{
	private static readonly RandomStringGenerator GenerateRandomString = RandomnessProviders.Secure.CreateRandomStringGenerator();

	public static readonly PersistentConfig DefaultMainNetConfig = new (
		Network : Network.Main,
		CoordinatorUri : Constants.CoordinatorUri,
		UseTor : GetDefaultTorMode(),
		TerminateTorOnExit : false,
		TorBridges : [],
		DownloadNewVersion : true,
		BitcoinRpcCredentialString : string.Empty,
		BitcoinRpcUri : Constants.DefaultMainNetBitcoinRpcUri,
		JsonRpcServerEnabled : false,
		JsonRpcUser : GenerateRandomString(12),
		JsonRpcPassword : GenerateRandomString(12),
		JsonRpcServerPrefixes : new (["http://127.0.0.1:37128/", "http://localhost:37128/"]),
		DustThreshold : Money.Coins(Constants.DefaultDustThreshold),
		EnableGpu : true,
		CoordinatorIdentifier : "CoinJoinCoordinatorIdentifier",
		ExchangeRateProvider : Constants.DefaultExchangeRateProvider,
		FeeRateEstimationProvider : Constants.DefaultFeeRateEstimationProvider,
		ExternalTransactionBroadcaster : Constants.DefaultExternalTransactionBroadcaster,
		MaxCoinJoinMiningFeeRate : Constants.DefaultMaxCoinJoinMiningFeeRate,
		AbsoluteMinInputCount : Constants.DefaultAbsoluteMinInputCount,
		MaxDaysInMempool : Constants.DefaultMaxDaysInMempool,
		ExperimentalFeatures: [],
		ConfigVersion : 4);

	public static readonly PersistentConfig DefaultTestNetConfig = DefaultMainNetConfig with
	{
		Network = Network.TestNet,
		CoordinatorUri = Constants.TestnetCoordinatorUri,
		BitcoinRpcCredentialString = string.Empty,
		BitcoinRpcUri = Constants.DefaultTestNetBitcoinRpcUri,
		JsonRpcServerEnabled = true,
		AbsoluteMinInputCount = Constants.AbsoluteMinInputCount,
		ExperimentalFeatures = new ValueList<string>(["scripting"]),
	};

	public static readonly PersistentConfig DefaultRegTestConfig = DefaultTestNetConfig with
	{
		Network = Network.RegTest,
		CoordinatorUri = Constants.RegTestCoordinatorUri,
		BitcoinRpcUri = Constants.DefaultRegTestBitcoinRpcUri,
	};

	public static readonly PersistentConfig DefaultSignetConfig = DefaultTestNetConfig with
	{
		Network = Bitcoin.Instance.Signet,
		CoordinatorUri = Constants.SignetCoordinatorUri,
		BitcoinRpcUri = Constants.DefaultSignetBitcoinRpcUri,
	};

	public static string ToFile(string filePath, PersistentConfig obj)
	{
		string jsonString = JsonEncoder.ToReadableString(obj, PersistentConfigEncode.PersistentConfig);

		// Write-then-rename, so a concurrent reader never sees a truncated file (which would be treated as corrupted).
		var tempFilePath = $"{filePath}.tmp";
		File.WriteAllText(tempFilePath, jsonString, Encoding.UTF8);
		File.Move(tempFilePath, filePath, overwrite: true);

		return jsonString;
	}

	public static void UpdateNetwork(string filePath, Network network)
	{
		var networkFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, "network");
		File.WriteAllText(networkFilePath, network.ToString());
	}

	/// <summary>
	/// Start argument that lets Wasabi start with the default settings when a config file can't be read:
	/// the unreadable file is moved aside, never overwritten. Without it, Wasabi stops and leaves the file as it is.
	/// </summary>
	public const string ResetUnreadableConfigArgument = "--reset-unreadable-config";

	/// <exception cref="UnreadableConfigException">The file exists but can't be read, and <paramref name="setAsideIfUnreadable"/> is <c>false</c>.</exception>
	public static IPersistentConfig LoadFile(string filePath, bool setAsideIfUnreadable = false)
	{
		try
		{
			using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
			var decoder = JsonDecoder.FromStream(PersistentConfigDecode.PersistentConfig);
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (FileNotFoundException)
		{
			var defaultConfig = CreateDefaultFile(filePath);
			Logger.LogInfo($"File did not exist. Created at path: '{filePath}'.");
			return defaultConfig;
		}
		catch (Exception ex) when (setAsideIfUnreadable)
		{
			var asideFilePath = $"{filePath}.unreadable-{DateTime.Now:yyyyMMddHHmmss}";
			File.Move(filePath, asideFilePath);
			var defaultConfig = CreateDefaultFile(filePath);
			Logger.LogWarning($"'{filePath}' could not be read ({ex.Message}). Moved it to '{asideFilePath}' and created a default version.");
			return defaultConfig;
		}
		catch (Exception ex)
		{
			throw new UnreadableConfigException(filePath, ex);
		}

		static PersistentConfig CreateDefaultFile(string configFilePath)
		{
			var defaultConfig = Path.GetFileName(configFilePath) switch
			{
				"Config.json" => DefaultMainNetConfig,
				"Config.TestNet.json" => DefaultTestNetConfig,
				"Config.RegTest.json" => DefaultRegTestConfig,
				"Config.Signet.json" => DefaultSignetConfig,
				_ => throw new ArgumentException($"The file '{configFilePath}' is not a valid config file name.")
			};

			ToFile(configFilePath, defaultConfig);
			UpdateNetwork(configFilePath, defaultConfig.Network);
			return defaultConfig;
		}
	}

	private static string GetDefaultTorMode()
	{
		// On Tails and Whonix, Tor is already running system-wide
		// We should only connect to it, not start our own instance
		if (PlatformInformation.IsTailsOS())
		{
			Logger.LogInfo("Detected Tails operating system. Setting Tor mode to 'Connect Only' by default.");
			return "EnabledOnlyRunning";
		}
		else if (PlatformInformation.IsWhonix())
		{
			Logger.LogInfo("Detected Whonix operating system. Setting Tor mode to 'Connect Only' by default.");
			return "EnabledOnlyRunning";
		}

		return "Enabled";
	}
}
