using System.IO;
using NBitcoin;
using System.Net;
using System.Text.Json;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

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
		EndPoint mainNetBitcoinCoreRpcEndPoint,
		EndPoint testNetBitcoinCoreRpcEndPoint,
		EndPoint regTestBitcoinCoreRpcEndPoint,
		string filterType)

	: base(filePath)
	{
		Network = Guard.NotNull(nameof(network), network);
		BitcoinRpcConnectionString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcConnectionString), bitcoinRpcConnectionString);

		MainNetBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(mainNetBitcoinCoreRpcEndPoint), mainNetBitcoinCoreRpcEndPoint);
		TestNetBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(testNetBitcoinCoreRpcEndPoint), testNetBitcoinCoreRpcEndPoint);
		RegTestBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(regTestBitcoinCoreRpcEndPoint), regTestBitcoinCoreRpcEndPoint);

		FilterType = filterType;
	}

	public Network Network { get; } = Network.Main;

	public string BitcoinRpcConnectionString { get; } = "user:password";

	public EndPoint MainNetBitcoinCoreRpcEndPoint { get; } = Constants.DefaultMainNetBitcoinCoreRpcEndPoint;

	public EndPoint TestNetBitcoinCoreRpcEndPoint { get; } = Constants.DefaultTestNetBitcoinCoreRpcEndPoint;

	public EndPoint RegTestBitcoinCoreRpcEndPoint { get; } = Constants.DefaultRegTestBitcoinCoreRpcEndPoint;

	public string FilterType { get; } = Constants.DefaultFilterType;

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
			var decoder = Serialization.JsonDecoder.FromStream(ConfigDecode.Config(filePath));
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (Exception ex)
		{
			var config = new Config(filePath);
			File.WriteAllTextAsync(filePath, config.EncodeAsJson());
			Logger.LogInfo($"{nameof(Config)} file has been deleted because it was corrupted. Recreated default version at path: `{filePath}`.");
			Logger.LogWarning(ex);
			return config;
		}
	}

	protected override string EncodeAsJson() =>
		Serialization.JsonEncoder.ToReadableString(this, ConfigEncode.Config);
}
