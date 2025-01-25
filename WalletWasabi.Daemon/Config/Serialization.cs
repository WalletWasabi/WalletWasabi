using System.Linq;
using System.Text.Json.Nodes;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;

namespace WalletWasabi.Daemon;

public static class PersistentConfigEncode
{
	public static JsonNode UseTor(string useTor) =>
		bool.TryParse(useTor, out var b)
			? Bool(b)
			: String(useTor);

	public static JsonNode PersistentConfig(PersistentConfig cfg) =>
		Object([
			("Network", Network(cfg.Network)),
			("MainNetBackendUri", String(cfg.MainNetBackendUri)),
			("TestNetBackendUri", String(cfg.TestNetBackendUri)),
			("RegTestBackendUri", String(cfg.RegTestBackendUri)),
			("MainNetCoordinatorUri", String(cfg.MainNetCoordinatorUri)),
			("TestNetCoordinatorUri", String(cfg.TestNetCoordinatorUri)),
			("RegTestCoordinatorUri", String(cfg.RegTestCoordinatorUri)),
			("UseTor", UseTor(cfg.UseTor)),
			("TerminateTorOnExit", Bool(cfg.TerminateTorOnExit)),
			("TorBridges", Array(cfg.TorBridges.Select(String))),
			("DownloadNewVersion", Bool(cfg.DownloadNewVersion)),
			("StartLocalBitcoinCoreOnStartup", Bool(cfg.StartLocalBitcoinCoreOnStartup)),
			("StopLocalBitcoinCoreOnShutdown", Bool(cfg.StopLocalBitcoinCoreOnShutdown)),
			("LocalBitcoinCoreDataDir", String(cfg.LocalBitcoinCoreDataDir)),
			("MainNetBitcoinP2pEndPoint", EndPoint(cfg.MainNetBitcoinP2pEndPoint, Constants.DefaultMainNetBitcoinP2pPort)),
			("TestNetBitcoinP2pEndPoint", EndPoint(cfg.TestNetBitcoinP2pEndPoint, Constants.DefaultTestNetBitcoinP2pPort)),
			("RegTestBitcoinP2pEndPoint", EndPoint(cfg.RegTestBitcoinP2pEndPoint, Constants.DefaultRegTestBitcoinP2pPort)),
			("JsonRpcServerEnabled", Bool(cfg.JsonRpcServerEnabled)),
			("JsonRpcUser", String(cfg.JsonRpcUser)),
			("JsonRpcPassword", String(cfg.JsonRpcPassword)),
			("JsonRpcServerPrefixes", Array(cfg.JsonRpcServerPrefixes.Select(String))),
			("DustThreshold", MoneyBitcoins(cfg.DustThreshold)),
			("EnableGpu", Bool(cfg.EnableGpu)),
			("CoordinatorIdentifier", String(cfg.CoordinatorIdentifier)),
			("ExchangeRateProvider", String(cfg.ExchangeRateProvider)),
			("FeeRateEstimationProvider", String(cfg.FeeRateEstimationProvider)),
			("MaxCoinJoinMiningFeeRate", Decimal(cfg.MaxCoinJoinMiningFeeRate)),
			("AbsoluteMinInputCount", Int(cfg.AbsoluteMinInputCount)),
			("ConfigVersion", Int(cfg.ConfigVersion))
		]);
}


public static class PersistentConfigDecode
{
	public static readonly Decoder<string> UseTor =
		OneOf([
			Decode.Bool.Map(x => x? "Enabled" : "Disabled"),
			Decode.String
		]);

	public static readonly Decoder<PersistentConfig> PersistentConfig =
		Object(get => new PersistentConfig
		{
			Network = get.Required("Network", Decode.Network),
			MainNetBackendUri = get.Required("MainNetBackendUri", Decode.String),
			TestNetBackendUri = get.Required("TestNetBackendUri", Decode.String),
			RegTestBackendUri = get.Required("RegTestBackendUri", Decode.String),
			MainNetCoordinatorUri = get.Required("MainNetCoordinatorUri", Decode.String),
			TestNetCoordinatorUri = get.Required("TestNetCoordinatorUri", Decode.String),
			RegTestCoordinatorUri = get.Required("RegTestCoordinatorUri", Decode.String),
			UseTor = get.Required("UseTor", UseTor),
			TerminateTorOnExit = get.Required("TerminateTorOnExit", Decode.Bool),
			TorBridges = get.Required("TorBridges", Decode.Array(Decode.String)),
			DownloadNewVersion = get.Required("DownloadNewVersion", Decode.Bool),
			StartLocalBitcoinCoreOnStartup = get.Required("StartLocalBitcoinCoreOnStartup", Decode.Bool),
			StopLocalBitcoinCoreOnShutdown = get.Required("StopLocalBitcoinCoreOnShutdown", Decode.Bool),
			LocalBitcoinCoreDataDir = get.Required("LocalBitcoinCoreDataDir", Decode.String),
			MainNetBitcoinP2pEndPoint = get.Required("MainNetBitcoinP2pEndPoint", Decode.EndPoint),
			TestNetBitcoinP2pEndPoint = get.Required("TestNetBitcoinP2pEndPoint", Decode.EndPoint),
			RegTestBitcoinP2pEndPoint = get.Required("RegTestBitcoinP2pEndPoint", Decode.EndPoint),
			JsonRpcServerEnabled = get.Required("JsonRpcServerEnabled", Decode.Bool),
			JsonRpcUser = get.Required("JsonRpcUser", Decode.String),
			JsonRpcPassword = get.Required("JsonRpcPassword", Decode.String),
			JsonRpcServerPrefixes = get.Required("JsonRpcServerPrefixes", Decode.Array(Decode.String)),
			DustThreshold = get.Required("DustThreshold", Decode.MoneyBitcoins),
			EnableGpu = get.Required("EnableGpu", Decode.Bool),
			ExchangeRateProvider = get.Optional("ExchangeRateProvider", Decode.String) ?? "Mempoolspace",
			FeeRateEstimationProvider = get.Optional("FeeRateEstimationProvider", Decode.String) ?? "BlockstreamInfo",
			CoordinatorIdentifier = get.Required("CoordinatorIdentifier", Decode.String),
			MaxCoinJoinMiningFeeRate = get.Required("MaxCoinJoinMiningFeeRate", Decode.Decimal),
			AbsoluteMinInputCount = get.Required("AbsoluteMinInputCount", Decode.Int),
			ConfigVersion = get.Required("ConfigVersion", Decode.Int)
		});
}
