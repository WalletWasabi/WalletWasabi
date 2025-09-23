using System;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;
using Network = NBitcoin.Network;

namespace WalletWasabi.Daemon;

public static class PersistentConfigEncode
{
	public static JsonNode UseTor(string useTor) =>
		bool.TryParse(useTor, out var b)
			? Bool(b)
			: String(useTor);

	public static JsonNode PersistentConfig(PersistentConfig cfg) =>
		Object([
			("BackendUri", String(cfg.IndexerUri)),
			("CoordinatorUri", String(cfg.CoordinatorUri)),
			("UseTor", UseTor(cfg.UseTor)),
			("TerminateTorOnExit", Bool(cfg.TerminateTorOnExit)),
			("TorBridges", Array(cfg.TorBridges.Select(String))),
			("DownloadNewVersion", Bool(cfg.DownloadNewVersion)),
			("UseBitcoinRpc", Bool(cfg.UseBitcoinRpc)),
			("BitcoinRpcCredentialString", String(cfg.BitcoinRpcCredentialString)),
			("BitcoinRpcEndPoint", String(cfg.BitcoinRpcUri)),
			("JsonRpcServerEnabled", Bool(cfg.JsonRpcServerEnabled)),
			("JsonRpcUser", String(cfg.JsonRpcUser)),
			("JsonRpcPassword", String(cfg.JsonRpcPassword)),
			("JsonRpcServerPrefixes", Array(cfg.JsonRpcServerPrefixes.Select(String))),
			("DustThreshold", MoneyBitcoins(cfg.DustThreshold)),
			("EnableGpu", Bool(cfg.EnableGpu)),
			("CoordinatorIdentifier", String(cfg.CoordinatorIdentifier)),
			("ExchangeRateProvider", String(cfg.ExchangeRateProvider)),
			("FeeRateEstimationProvider", String(cfg.FeeRateEstimationProvider)),
			("ExternalTransactionBroadcaster", String(cfg.ExternalTransactionBroadcaster)),
			("MaxCoinJoinMiningFeeRate", Decimal(cfg.MaxCoinJoinMiningFeeRate)),
			("AbsoluteMinInputCount", Int(cfg.AbsoluteMinInputCount)),
			("MaxDaysInMempool", Int(cfg.MaxDaysInMempool)),
			("ExperimentalFeatures", Array(cfg.ExperimentalFeatures.Select(String))),
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

	public static Decoder<ValueList<T>> ValueList<T>(Decoder<T> decoder) where T : IEquatable<T> =>
		Array(decoder).Map(x => new ValueList<T>(x));

	private static IPEndPoint DefaultEndPoint = new (IPAddress.None, 0);

	public static readonly Decoder<PersistentConfig> PersistentConfigPost2_6_0 =
		Object(get => new PersistentConfig(
			Network: Network.Main, // Network is not part of the config
			IndexerUri : get.Required("BackendUri", Decode.String),
			CoordinatorUri : get.Required("CoordinatorUri", Decode.String),
			UseTor : get.Required("UseTor", UseTor),
			TerminateTorOnExit : get.Required("TerminateTorOnExit", Decode.Bool),
			TorBridges : get.Required("TorBridges", ValueList(Decode.String)),
			DownloadNewVersion : get.Required("DownloadNewVersion", Decode.Bool),
			UseBitcoinRpc : get.Optional("UseBitcoinRpc", Decode.Bool, false),
			BitcoinRpcCredentialString : get.Optional("BitcoinRpcCredentialString", Decode.String) ?? "",
			BitcoinRpcUri : get.Optional("BitcoinRpcEndPoint", Decode.String) ?? "",
			JsonRpcServerEnabled : get.Required("JsonRpcServerEnabled", Decode.Bool),
			JsonRpcUser : get.Required("JsonRpcUser", Decode.String),
			JsonRpcPassword : get.Required("JsonRpcPassword", Decode.String),
			JsonRpcServerPrefixes : get.Required("JsonRpcServerPrefixes", ValueList(Decode.String)),
			DustThreshold : get.Required("DustThreshold", Decode.MoneyBitcoins),
			EnableGpu : get.Required("EnableGpu", Decode.Bool),
			ExchangeRateProvider : get.Optional("ExchangeRateProvider", Decode.String) ?? "Mempoolspace",
			FeeRateEstimationProvider : get.Optional("FeeRateEstimationProvider", Decode.String) ?? "BlockstreamInfo",
			ExternalTransactionBroadcaster : get.Optional("ExternalTransactionBroadcaster", Decode.String) ?? "MempoolSpace",
			CoordinatorIdentifier : get.Required("CoordinatorIdentifier", Decode.String),
			MaxCoinJoinMiningFeeRate : get.Required("MaxCoinJoinMiningFeeRate", Decode.Decimal),
			AbsoluteMinInputCount : get.Required("AbsoluteMinInputCount", Decode.Int),
			MaxDaysInMempool : get.Optional("MaxDaysInMempool", Decode.Int, Constants.DefaultMaxDaysInMempool),
			ExperimentalFeatures: get.Optional("ExperimentalFeatures", ValueList(Decode.String)) ?? Helpers.ValueList<string>.Empty,
			ConfigVersion : get.Required("ConfigVersion", Decode.Int)
		));

	public static readonly Decoder<PersistentConfigPrev2_6_0> PersistentConfigPrev2_6_0 =
		Object(get => new PersistentConfigPrev2_6_0(
			get.Required("MainNetBackendUri", Decode.String),
			get.Required("TestNetBackendUri", Decode.String),
			get.Required("RegTestBackendUri", Decode.String),
			get.Required("MainNetCoordinatorUri", Decode.String),
			get.Required("TestNetCoordinatorUri", Decode.String),
			get.Required("RegTestCoordinatorUri", Decode.String),
			get.Required("UseTor", UseTor),
			get.Required("TerminateTorOnExit", Decode.Bool),
			get.Required("TorBridges", Decode.Array(Decode.String)),
			get.Required("DownloadNewVersion", Decode.Bool),
			get.Optional("UseBitcoinRpc", Decode.Bool, false),
			get.Optional("MainNetBitcoinRpcCredentialString", Decode.String) ?? "",
			get.Optional("TestNetBitcoinRpcCredentialString", Decode.String) ?? "",
			get.Optional("RegTestBitcoinRpcCredentialString", Decode.String) ?? "",
			get.Optional("MainNetBitcoinRpcEndPoint", Decode.EndPoint) ?? DefaultEndPoint,
			get.Optional("TestNetBitcoinRpcEndPoint", Decode.EndPoint) ?? DefaultEndPoint,
			get.Optional("RegTestBitcoinRpcEndPoint", Decode.EndPoint) ?? DefaultEndPoint,
			get.Required("JsonRpcServerEnabled", Decode.Bool),
			get.Required("JsonRpcUser", Decode.String),
			get.Required("JsonRpcPassword", Decode.String),
			get.Required("JsonRpcServerPrefixes", Decode.Array(Decode.String)),
			get.Required("DustThreshold", Decode.MoneyBitcoins),
			get.Required("EnableGpu", Decode.Bool),
			get.Required("CoordinatorIdentifier", Decode.String),
			get.Optional("ExchangeRateProvider", Decode.String) ?? "Mempoolspace",
			get.Optional("FeeRateEstimationProvider", Decode.String) ?? "BlockstreamInfo",
			get.Optional("ExternalTransactionBroadcaster", Decode.String) ?? "MempoolSpace",
			get.Required("MaxCoinJoinMiningFeeRate", Decode.Decimal),
			get.Required("AbsoluteMinInputCount", Decode.Int),
			get.Optional("MaxDaysInMempool", Decode.Int, Constants.DefaultMaxDaysInMempool),
			get.Required("ConfigVersion", Decode.Int)
		));

	public static readonly Decoder<IPersistentConfig> PersistentConfig =
		OneOf([
			PersistentConfigPrev2_6_0.Map(IPersistentConfig (x) => x),
			PersistentConfigPost2_6_0.Map(IPersistentConfig (x) => x)
		]);
}
