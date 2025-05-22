using System;
using System.IO;
using NBitcoin;
using System.Net;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Daemon;

public interface IPersistentConfig;

public record PersistentConfig(
	Network Network,
	string IndexerUri,
	string CoordinatorUri,
	string UseTor,
	bool TerminateTorOnExit,
	ValueList<string> TorBridges,
	bool DownloadNewVersion,
	bool UseBitcoinRpc,
	string BitcoinRpcCredentialString,
	string BitcoinRpcUri,
	bool JsonRpcServerEnabled,
	string JsonRpcUser,
	string JsonRpcPassword,
	ValueList<string> JsonRpcServerPrefixes,
	Money DustThreshold,
	bool EnableGpu,
	string CoordinatorIdentifier,
	string ExchangeRateProvider,
	string FeeRateEstimationProvider,
	string ExternalTransactionBroadcaster,
	decimal MaxCoinJoinMiningFeeRate,
	int AbsoluteMinInputCount,
	int MaxDaysInMempool,
	int ConfigVersion
	) : IPersistentConfig
{
	public string GetConfigFileName() =>
		Network switch
		{
			_ when Network == Network.Main => "Config.json",
			_ when Network == Network.TestNet => "Config.TestNet.json",
			_ when Network == Network.RegTest => "Config.RegTest.json",
			_ => throw new NotSupportedException("Unsupported network")
		};
}

public record PersistentConfigPrev2_6_0(
	string MainNetIndexerUri,
	string TestNetIndexerUri,
	string RegTestIndexerUri,
	string MainNetCoordinatorUri,
	string TestNetCoordinatorUri,
	string RegTestCoordinatorUri,
	string UseTor,
	bool TerminateTorOnExit,
	string[] TorBridges,
	bool DownloadNewVersion,
	bool UseBitcoinRpc,
	string MainNetBitcoinRpcCredentialString,
	string TestNetBitcoinRpcCredentialString,
	string RegTestBitcoinRpcCredentialString,
	EndPoint MainNetBitcoinRpcEndPoint,
	EndPoint TestNetBitcoinRpcEndPoint,
	EndPoint RegTestBitcoinRpcEndPoint,
	bool JsonRpcServerEnabled,
	string JsonRpcUser,
	string JsonRpcPassword,
	string[] JsonRpcServerPrefixes,
	Money DustThreshold,
	bool EnableGpu,
	string CoordinatorIdentifier,
	string ExchangeRateProvider,
	string FeeRateEstimationProvider,
	string ExternalTransactionBroadcaster,
	decimal MaxCoinJoinMiningFeeRate,
	int AbsoluteMinInputCount,
	int MaxDaysInMempool,
	int ConfigVersion) : IPersistentConfig
{
	public PersistentConfigPrev2_6_0 Migrate() =>
		MigrateMaxCoordinationFeeRate()
		.MigrateOldDefaultBackendUris()
		.MigrateP2pToRpcConnection() with
		{
			ConfigVersion = 2
		};

	private PersistentConfigPrev2_6_0 MigrateMaxCoordinationFeeRate() => this;

	private PersistentConfigPrev2_6_0 MigrateOldDefaultBackendUris()
	{
		if (MainNetIndexerUri == "https://wasabiwallet.io/" || TestNetIndexerUri == "https://wasabiwallet.co/")
		{
			return this with
			{
				MainNetIndexerUri = "https://api.wasabiwallet.io/",
				TestNetIndexerUri = "https://api.wasabiwallet.co/",
			};
		}

		return this;
	}

	private PersistentConfigPrev2_6_0 MigrateP2pToRpcConnection()
	{
		if (ConfigVersion >= 2)
		{
			return this;
		}

		static string? GetRpcCredentialString(BitcoinConfig config, string network, string bitcoindatadir) =>
			( config.GetSettingOrNull("rpcuser", network)
			, config.GetSettingOrNull("rpcpassword", network)
			, config.GetSettingOrNull("rpccookiefile", network)
			) switch
			{
				({ } rpcUser, { } rpcPassword, _) => $"{rpcUser}:{rpcPassword}",
				( _, null, { } cookieFilePath) => $"cookiefile={cookieFilePath}",
				( _, null, _) => $"cookiefile={bitcoindatadir}/.cookie",
				_ => null
			};
		static EndPoint GetRpcEndpoint(BitcoinConfig config, string network, int defaultPort) =>
			(config.GetSettingOrNull("rpcbind", network), config.GetSettingOrNull("rpcport", network)) switch
			{
				({} host, {} port) when EndPointParser.TryParse($"{host}:{port}", out var endPoint) => endPoint,
				({} host, null) when EndPointParser.TryParse($"{host}:{defaultPort}", out var endPoint) => endPoint,
				(null, {} port) when int.TryParse(port, out var intPort) => new IPEndPoint(IPAddress.Loopback, intPort),
				_ => new IPEndPoint(IPAddress.Loopback, defaultPort)
			};

		var defaultBitcoinDataDir = EnvironmentHelpers.GetDefaultBitcoinDataDir();
		if (string.IsNullOrWhiteSpace(defaultBitcoinDataDir))
		{
			return this;
		}

		var configPath = Path.Combine(defaultBitcoinDataDir, "bitcoin.conf");
		if (!File.Exists(configPath))
		{
			return this;
		}
		try
		{
			var bitcoinConfig = File.ReadAllText(configPath);
			var config = BitcoinConfig.Parse(bitcoinConfig);

			return this with
			{
				MainNetBitcoinRpcEndPoint = GetRpcEndpoint(config, "main", Network.Main.RPCPort),
				TestNetBitcoinRpcEndPoint = GetRpcEndpoint(config, "testnet4", Network.TestNet.RPCPort),
				RegTestBitcoinRpcEndPoint = GetRpcEndpoint(config, "regtest", Network.RegTest.RPCPort),

				MainNetBitcoinRpcCredentialString = GetRpcCredentialString(config, "main", defaultBitcoinDataDir) ?? "",
				TestNetBitcoinRpcCredentialString = GetRpcCredentialString(config, "testnet4", defaultBitcoinDataDir) ?? "",
				RegTestBitcoinRpcCredentialString = GetRpcCredentialString(config, "regtest", defaultBitcoinDataDir) ?? "",
			};
		}
		catch (IOException e)
		{
			Logger.LogError("It was not possible to read the bitcoin's config file.", e);
			return this;
		}
	}
}
