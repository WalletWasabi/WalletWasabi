using System.IO;
using System.Text.Json.Nodes;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;

namespace WalletWasabi.Backend;

public static class ConfigEncode
{
	public static JsonNode Config(Config cfg) =>
		Object([
			("Network", Network(cfg.Network) ),
			("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString) ),
			("MainNetBitcoinP2pEndPoint", EndPoint(cfg.MainNetBitcoinP2pEndPoint, Constants.DefaultMainNetBitcoinP2pPort) ),
			("TestNetBitcoinP2pEndPoint", EndPoint(cfg.TestNetBitcoinP2pEndPoint, Constants.DefaultTestNetBitcoinP2pPort) ),
			("RegTestBitcoinP2pEndPoint", EndPoint(cfg.RegTestBitcoinP2pEndPoint, Constants.DefaultRegTestBitcoinP2pPort) ),
			("MainNetBitcoinCoreRpcEndPoint", EndPoint(cfg.MainNetBitcoinCoreRpcEndPoint, Constants.DefaultMainNetBitcoinCoreRpcPort) ),
			("TestNetBitcoinCoreRpcEndPoint", EndPoint(cfg.TestNetBitcoinCoreRpcEndPoint, Constants.DefaultTestNetBitcoinCoreRpcPort) ),
			("RegTestBitcoinCoreRpcEndPoint", EndPoint(cfg.RegTestBitcoinCoreRpcEndPoint, Constants.DefaultRegTestBitcoinCoreRpcPort) ),
		]);
}

public static class ConfigDecode
{
	public static Decoder<Config> Config(string filePath) =>
		Object(get => new Config(
			filePath,
			get.Required("Network", Decode.Network ),
			get.Required("BitcoinRpcConnectionString", Decode.String ),
			get.Required("MainNetBitcoinP2pEndPoint", Decode.EndPoint ),
			get.Required("TestNetBitcoinP2pEndPoint", Decode.EndPoint ),
			get.Required("RegTestBitcoinP2pEndPoint", Decode.EndPoint ),
			get.Required("MainNetBitcoinCoreRpcEndPoint", Decode.EndPoint ),
			get.Required("TestNetBitcoinCoreRpcEndPoint", Decode.EndPoint ),
			get.Required("RegTestBitcoinCoreRpcEndPoint", Decode.EndPoint )
		));
}
