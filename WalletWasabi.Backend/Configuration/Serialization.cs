using System.Text.Json.Nodes;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;

namespace WalletWasabi.Backend.Configuration;

public static class ConfigEncode
{
	public static JsonNode Config(Config cfg) =>
		Object([
			("Network", Network(cfg.Network) ),
			("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString) ),
			("MainNetBitcoinCoreRpcEndPoint", String(cfg.MainNetBitcoinRpcUri) ),
			("TestNetBitcoinCoreRpcEndPoint", String(cfg.TestNetBitcoinRpcUri) ),
			("RegTestBitcoinCoreRpcEndPoint", String(cfg.RegTestBitcoinRpcUri) ),
			("FilterType", Constants.DefaultFilterType)
		]);
}

public static class ConfigDecode
{
	public static Decoder<Config> Config(string filePath) =>
		Object(get => new Config(
			filePath,
			get.Required("Network", Decode.Network ),
			get.Required("BitcoinRpcConnectionString", Decode.String ),
			get.Required("MainNetBitcoinCoreRpcEndPoint", Decode.String ),
			get.Required("TestNetBitcoinCoreRpcEndPoint", Decode.String ),
			get.Required("RegTestBitcoinCoreRpcEndPoint", Decode.String ),
			get.Optional("FilterType", Decode.String) ?? Constants.DefaultFilterType
		));
}
