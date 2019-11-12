using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Endpointing
{
	public class EndPointStrategy
	{
		public static EndPointStrategy Random
			=> new EndPointStrategy(EndPointStrategyType.Random, new IPEndPoint(IPAddress.Loopback, PortFinder.GetRandomPorts(1)[0]));

		public static EndPointStrategy Custom(EndPoint endPoint)
			=> new EndPointStrategy(EndPointStrategyType.Custom, endPoint);

		public static EndPointStrategy Default(Network network, EndPointType endPointType)
		{
			var port = endPointType == EndPointType.Rpc ? network.RPCPort : network.DefaultPort;
			return new EndPointStrategy(EndPointStrategyType.Default, new IPEndPoint(IPAddress.Loopback, port));
		}

		private EndPointStrategy(EndPointStrategyType endPointStrategyType, EndPoint endPoint)
		{
			EndPointStrategyType = endPointStrategyType;
			EndPoint = Guard.NotNull(nameof(endPoint), endPoint);
		}

		public EndPointStrategyType EndPointStrategyType { get; }
		public EndPoint EndPoint { get; }
	}
}
