using System.IO;
using NBitcoin;
using System.Net;
using System.Text.Json;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.Backend;

public class Config : ConfigBase
{
	public Config(string filePath) : base(filePath)
	{
	}

	public Config(
		string filePath,
		Network network,
		string bitcoinRpcConnectionString,
		EndPoint mainNetBitcoinP2pEndPoint,
		EndPoint testNetBitcoinP2pEndPoint,
		EndPoint regTestBitcoinP2pEndPoint,
		EndPoint mainNetBitcoinCoreRpcEndPoint,
		EndPoint testNetBitcoinCoreRpcEndPoint,
		EndPoint regTestBitcoinCoreRpcEndPoint)
	: base(filePath)
	{
		Network = Guard.NotNull(nameof(network), network);
		BitcoinRpcConnectionString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcConnectionString), bitcoinRpcConnectionString);

		MainNetBitcoinP2pEndPoint = Guard.NotNull(nameof(mainNetBitcoinP2pEndPoint), mainNetBitcoinP2pEndPoint);
		TestNetBitcoinP2pEndPoint = Guard.NotNull(nameof(testNetBitcoinP2pEndPoint), testNetBitcoinP2pEndPoint);
		RegTestBitcoinP2pEndPoint = Guard.NotNull(nameof(regTestBitcoinP2pEndPoint), regTestBitcoinP2pEndPoint);

		MainNetBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(mainNetBitcoinCoreRpcEndPoint), mainNetBitcoinCoreRpcEndPoint);
		TestNetBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(testNetBitcoinCoreRpcEndPoint), testNetBitcoinCoreRpcEndPoint);
		RegTestBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(regTestBitcoinCoreRpcEndPoint), regTestBitcoinCoreRpcEndPoint);
	}

	public Network Network { get; } = Network.Main;

	public string BitcoinRpcConnectionString { get; } = "user:password";

	public EndPoint MainNetBitcoinP2pEndPoint { get; } = Constants.DefaultMainNetBitcoinP2PEndPoint;

	public EndPoint TestNetBitcoinP2pEndPoint { get; } = Constants.DefaultTestNetBitcoinP2PEndPoint;

	public EndPoint RegTestBitcoinP2pEndPoint { get; } = Constants.DefaultRegTestBitcoinP2PEndPoint;

	public EndPoint MainNetBitcoinCoreRpcEndPoint { get; } = Constants.DefaultMainNetBitcoinCoreRpcEndPoint;

	public EndPoint TestNetBitcoinCoreRpcEndPoint { get; } = Constants.DefaultTestNetBitcoinCoreRpcEndPoint;

	public EndPoint RegTestBitcoinCoreRpcEndPoint { get; } = Constants.DefaultRegTestBitcoinCoreRpcEndPoint;

	public EndPoint GetBitcoinP2pEndPoint()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinP2pEndPoint;
		}
		else if (Network == Network.TestNet)
		{
			return TestNetBitcoinP2pEndPoint;
		}
		else if (Network == Network.RegTest)
		{
			return RegTestBitcoinP2pEndPoint;
		}
		else
		{
			throw new NotSupportedNetworkException(Network);
		}
	}

	public EndPoint GetBitcoinCoreRpcEndPoint()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinCoreRpcEndPoint;
		}
		else if (Network == Network.TestNet)
		{
			return TestNetBitcoinCoreRpcEndPoint;
		}
		else if (Network == Network.RegTest)
		{
			return RegTestBitcoinCoreRpcEndPoint;
		}
		else
		{
			throw new NotSupportedNetworkException(Network);
		}
	}

	public static Config LoadFile(string filePath)
	{
		try
		{
			using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
			var decoder = Decode.FromStream(ConfigDecode.Config(filePath));
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (Exception ex)
		{
			var config = new Config(filePath);
			File.WriteAllTextAsync(filePath, ConfigEncode.Config(config).ToJsonString());
			Logger.LogInfo($"{nameof(Config)} file has been deleted because it was corrupted. Recreated default version at path: `{filePath}`.");
			Logger.LogWarning(ex);
			return config;
		}
	}

	protected override string EncodeAsJson() =>
		ConfigEncode.Config(this).ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		});
}
