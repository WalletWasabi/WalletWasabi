using System;
using System.IO;
using NBitcoin;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend.Configuration;

public class Config : ConfigBase
{
	public Config(string filePath) : base(filePath)
	{
	}

	public Config(
		string filePath,
		Network network,
		string bitcoinRpcConnectionString,
		string mainNetBitcoinRpcUri,
		string testNetBitcoinRpcUri,
		string regTestBitcoinRpcUri,
		string filterType)

	: base(filePath)
	{
		Network = Guard.NotNull(nameof(network), network);
		BitcoinRpcConnectionString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcConnectionString), bitcoinRpcConnectionString);

		MainNetBitcoinRpcUri = Guard.NotNull(nameof(mainNetBitcoinRpcUri), mainNetBitcoinRpcUri);
		TestNetBitcoinRpcUri = Guard.NotNull(nameof(testNetBitcoinRpcUri), testNetBitcoinRpcUri);
		RegTestBitcoinRpcUri = Guard.NotNull(nameof(regTestBitcoinRpcUri), regTestBitcoinRpcUri);

		FilterType = filterType;
	}

	public Network Network { get; } = Network.Main;

	public string MainNetBitcoinRpcUri { get; } = Constants.DefaultMainNetBitcoinRpcUri;

	public string TestNetBitcoinRpcUri { get; } = Constants.DefaultTestNetBitcoinRpcUri;

	public string RegTestBitcoinRpcUri { get; } = Constants.DefaultRegTestBitcoinRpcUri;

	public string BitcoinRpcConnectionString { get; } = "user:password";

	public string FilterType { get; } = Constants.DefaultFilterType;

	public string GetBitcoinRpcUri() =>
		Network switch
		{
			_ when Network == Network.Main => MainNetBitcoinRpcUri,
			_ when Network == Network.TestNet => TestNetBitcoinRpcUri,
			_ when Network == Network.RegTest => RegTestBitcoinRpcUri,
			_ => throw new NotSupportedNetworkException(Network)
		};

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
