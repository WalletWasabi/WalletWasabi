using System.Net;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.BitcoinCore.Endpointing;

public class EndPointStrategy
{
	private EndPointStrategy(EndPointStrategyType endPointStrategyType, EndPoint endPoint)
	{
		EndPointStrategyType = endPointStrategyType;
		EndPoint = Guard.NotNull(nameof(endPoint), endPoint);
	}

	public EndPointStrategyType EndPointStrategyType { get; }
	public EndPoint EndPoint { get; }

	public static EndPointStrategy Random
		=> new(EndPointStrategyType.Random, new IPEndPoint(IPAddress.Loopback, PortFinder.GetRandomPorts(1)[0]));

	public static EndPointStrategy Default(Network network, EndPointType endPointType)
	{
		var port = endPointType == EndPointType.Rpc ? network.RPCPort : network.DefaultPort;
		return new EndPointStrategy(EndPointStrategyType.Default, new IPEndPoint(IPAddress.Loopback, port));
	}
}
