using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Net;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend;

[JsonObject(MemberSerialization.OptIn)]
public class Config : ConfigBase
{
	public Config() : base()
	{
	}

	public Config(string filePath) : base(filePath)
	{
	}

	public Config(
		Network network,
		string bitcoinRpcConnectionString,
		EndPoint mainNetBitcoinP2pEndPoint,
		EndPoint testNetBitcoinP2pEndPoint,
		EndPoint regTestBitcoinP2pEndPoint,
		EndPoint mainNetBitcoinCoreRpcEndPoint,
		EndPoint testNetBitcoinCoreRpcEndPoint,
		EndPoint regTestBitcoinCoreRpcEndPoint)
		: base()
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

	[JsonProperty(PropertyName = "Network")]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; private set; } = Network.Main;

	[DefaultValue("user:password")]
	[JsonProperty(PropertyName = "BitcoinRpcConnectionString", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string BitcoinRpcConnectionString { get; private set; } = "user:password";

	[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
	public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
	public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
	public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

	[JsonProperty(PropertyName = "MainNetBitcoinCoreRpcEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinCoreRpcPort)]
	public EndPoint MainNetBitcoinCoreRpcEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinCoreRpcPort);

	[JsonProperty(PropertyName = "TestNetBitcoinCoreRpcEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinCoreRpcPort)]
	public EndPoint TestNetBitcoinCoreRpcEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinCoreRpcPort);

	[JsonProperty(PropertyName = "RegTestBitcoinCoreRpcEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinCoreRpcPort)]
	public EndPoint RegTestBitcoinCoreRpcEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinCoreRpcPort);

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
}
