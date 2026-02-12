using System;
using System.IO;
using System.Text;
using NBitcoin;
using WalletWasabi.Daemon;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.Bases;

public static class PersistentConfigManager
{
	public static readonly PersistentConfig DefaultMainNetConfig = new (
		Network : Network.Main,
		IndexerUri : Constants.IndexerUri,
		CoordinatorUri : Constants.CoordinatorUri,
		UseTor : "Enabled",
		TerminateTorOnExit : false,
		TorBridges : [],
		DownloadNewVersion : true,
		UseBitcoinRpc : false,
		BitcoinRpcCredentialString : string.Empty,
		BitcoinRpcUri : Constants.DefaultMainNetBitcoinRpcUri.ToString(),
		JsonRpcServerEnabled : false,
		JsonRpcUser : string.Empty,
		JsonRpcPassword : string.Empty,
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
		ExperimentalFeatures: ValueList<string>.Empty,
		ConfigVersion : 2);

	public static readonly PersistentConfig DefaultTestNetConfig = DefaultMainNetConfig with
	{
		Network = Network.TestNet,
		IndexerUri = Constants.TestnetIndexerUri,
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
		IndexerUri = Constants.RegTestIndexerUri,
		CoordinatorUri = Constants.RegTestCoordinatorUri,
		BitcoinRpcUri = Constants.DefaultRegTestBitcoinRpcUri,
	};

	public static readonly PersistentConfig DefaultSignetConfig = DefaultTestNetConfig with
	{
		Network = Bitcoin.Instance.Signet,
		IndexerUri = Constants.SignetIndexerUri,
		CoordinatorUri = Constants.SignetCoordinatorUri,
		BitcoinRpcUri = Constants.DefaultSignetBitcoinRpcUri,
	};

	public static string ToFile(string filePath, PersistentConfig obj)
	{
		string jsonString = JsonEncoder.ToReadableString(obj, PersistentConfigEncode.PersistentConfig);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
		var networkFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, "network");
		File.WriteAllText(networkFilePath, obj.Network.ToString());
		return jsonString;
	}

	public static IPersistentConfig LoadFile(string filePath)
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
			var defaultConfig = GetDefaultPersistentConfigByFileName(filePath);

			ToFile(filePath, defaultConfig);
			Logger.LogInfo($"File did not exist. Created at path: '{filePath}'.");
			return defaultConfig;
		}
		catch (Exception ex)
		{
			var defaultConfig = GetDefaultPersistentConfigByFileName(filePath);

			ToFile(filePath, defaultConfig);
			Logger.LogInfo($"{nameof(Config)} file has been deleted because it was corrupted. Recreated default version at path: '{filePath}'.");
			Logger.LogWarning(ex);
			return defaultConfig;
		}

		static PersistentConfig GetDefaultPersistentConfigByFileName(string configFilePath) =>
			Path.GetFileName(configFilePath) switch
			{
				"Config.json" => DefaultMainNetConfig,
				"Config.TestNet.json" => DefaultTestNetConfig,
				"Config.RegTest.json" => DefaultRegTestConfig,
				"Config.Signet.json" => DefaultSignetConfig,
				_ => throw new ArgumentException($"The file '{configFilePath}' is not a valid config file name.")
			};
	}
}
