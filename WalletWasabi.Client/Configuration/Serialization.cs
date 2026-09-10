using System;
using System.Linq;
using System.Text.Json.Nodes;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;
using Network = NBitcoin.Network;

namespace WalletWasabi.Client.Configuration;

public static class PersistentConfigEncode
{
	public static JsonNode UseTor(string useTor) =>
		bool.TryParse(useTor, out var b)
			? Bool(b)
			: String(useTor);

	public static JsonNode PersistentConfig(PersistentConfig cfg) =>
		Object([
			("CoordinatorUri", String(cfg.CoordinatorUri)),
			("UseTor", UseTor(cfg.UseTor)),
			("TerminateTorOnExit", Bool(cfg.TerminateTorOnExit)),
			("TorBridges", Array(cfg.TorBridges.Select(String))),
			("DownloadNewVersion", Bool(cfg.DownloadNewVersion)),
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
			("ConfigVersion", Int(4))
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

	public static readonly Decoder<PersistentConfig> PersistentConfig2_8_0 =
		Object(get =>
		{
			var ver = get.Required("ConfigVersion", Decode.Int);
			if (ver == 4)
			{
				return new PersistentConfig(
					Network: Network.Main, // Network is not part of the config
					CoordinatorUri: get.Required("CoordinatorUri", Decode.String),
					UseTor: get.Required("UseTor", UseTor),
					TerminateTorOnExit: get.Required("TerminateTorOnExit", Decode.Bool),
					TorBridges: get.Required("TorBridges", ValueList(Decode.String)),
					DownloadNewVersion: get.Required("DownloadNewVersion", Decode.Bool),
					BitcoinRpcCredentialString: get.Optional("BitcoinRpcCredentialString", Decode.String) ?? "",
					BitcoinRpcUri: get.Optional("BitcoinRpcEndPoint", Decode.String) ?? "",
					JsonRpcServerEnabled: get.Required("JsonRpcServerEnabled", Decode.Bool),
					JsonRpcUser: get.Required("JsonRpcUser", Decode.String),
					JsonRpcPassword: get.Required("JsonRpcPassword", Decode.String),
					JsonRpcServerPrefixes: get.Required("JsonRpcServerPrefixes", ValueList(Decode.String)),
					DustThreshold: get.Required("DustThreshold", Decode.MoneyBitcoins),
					EnableGpu: get.Required("EnableGpu", Decode.Bool),
					ExchangeRateProvider: get.Optional("ExchangeRateProvider", Decode.String) ?? "Mempoolspace",
					FeeRateEstimationProvider: get.Optional("FeeRateEstimationProvider", Decode.String) ?? "BlockstreamInfo",
					ExternalTransactionBroadcaster: get.Optional("ExternalTransactionBroadcaster", Decode.String) ?? "MempoolSpace",
					CoordinatorIdentifier: get.Required("CoordinatorIdentifier", Decode.String),
					MaxCoinJoinMiningFeeRate: get.Required("MaxCoinJoinMiningFeeRate", Decode.Decimal),
					AbsoluteMinInputCount: get.Required("AbsoluteMinInputCount", Decode.Int),
					MaxDaysInMempool: get.Optional("MaxDaysInMempool", Decode.Int, Constants.DefaultMaxDaysInMempool),
					ExperimentalFeatures: get.Optional("ExperimentalFeatures", ValueList(Decode.String)) ?? [],
					ConfigVersion: get.Required("ConfigVersion", Decode.Int)
				);
			}
			else
			{
				get.Errors.Add("Config file not compatible with v2.8.0");
				return null!;
			}
		});

	public static readonly Decoder<PersistentConfig_2_6_0> PersistentConfig2_6_0 =
		Object(get => new PersistentConfig_2_6_0(
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
			ExperimentalFeatures: get.Optional("ExperimentalFeatures", ValueList(Decode.String)) ?? [],
			ConfigVersion : get.Required("ConfigVersion", Decode.Int)
		));

	public static readonly Decoder<IPersistentConfig> PersistentConfig =
		OneOf([
			PersistentConfig2_8_0.Map(IPersistentConfig (x) => x),
			PersistentConfig2_6_0.Map(IPersistentConfig (x) => x),
		]);
}
