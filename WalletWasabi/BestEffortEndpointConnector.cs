using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using NBitcoin.Socks;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi
{
	public class BestEffortEndpointConnector : IEnpointConnector
	{
		public BestEffortEndpointConnector(long maxNonOnionConnectionCount)
			: this(new EffortState(maxNonOnionConnectionCount))
		{
		}

		private BestEffortEndpointConnector(EffortState state)
		{
			State = state;
		}

		public EffortState State { get; private set; }

		public void UpdateConnectedNodesCounter(int connectedNodes)
		{
			State.ConnectedNodesCount = connectedNodes;
		}

		public IEnpointConnector Clone()
		{
			return new BestEffortEndpointConnector(State);
		}

		public virtual async Task ConnectSocket(Socket socket, EndPoint endpoint, NodeConnectionParameters nodeConnectionParameters, CancellationToken cancellationToken)
		{
			var isTor = endpoint.IsTor();

			var socksSettings = nodeConnectionParameters.TemplateBehaviors.Find<SocksSettingsBehavior>();
			var socketEndpoint = endpoint;
			var useSocks = isTor || socksSettings?.OnlyForOnionHosts is false;
			if (useSocks)
			{
				if (socksSettings?.SocksEndpoint is null)
				{
					throw new InvalidOperationException("SocksSettingsBehavior.SocksEndpoint is not set but the connection is expecting using socks proxy");
				}
				if (!isTor && State.AllowOnlyTorEndpoints)
				{
					throw new InvalidOperationException($"The Endpoint connector is configured to allow only Tor endpoints and the '{endpoint}' enpoint is not one");
				}

				socketEndpoint = socksSettings.SocksEndpoint;
			}

			if (socketEndpoint is IPEndPoint mappedv4 && mappedv4.Address.IsIPv4MappedToIPv6Ex())
			{
				socketEndpoint = new IPEndPoint(mappedv4.Address.MapToIPv4Ex(), mappedv4.Port);
			}
			await socket.ConnectAsync(socketEndpoint, cancellationToken).ConfigureAwait(false);

			if (useSocks)
			{
				await SocksHelper.Handshake(socket, endpoint, GenerateCredentials(), cancellationToken).ConfigureAwait(false);
			}
		}

		private NetworkCredential GenerateCredentials()
		{
			const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			var identity = new string(Enumerable.Repeat(Chars, 21)
				.Select(s => s[(int)(RandomUtils.GetUInt32() % s.Length)]).ToArray());
			return new NetworkCredential(identity, identity);
		}

		// A class to share state between all the BestEffortEndpointConnector connectors.
		// This is necessary because NBitcoin clones the connectors for every new node connection
		// attempt using the original connector.
		public class EffortState
		{
			private bool _allowAnyConnetionType;

			public EffortState(long maxNonOnionConnectionCount)
			{
				MaxNonOnionConnectionCount = maxNonOnionConnectionCount;
			}

			public long ConnectedNodesCount { get; set; }

			public bool AllowOnlyTorEndpoints
			{
				get
				{
					var allowAnyConnetionType = ConnectedNodesCount <= MaxNonOnionConnectionCount;

					if (_allowAnyConnetionType != allowAnyConnetionType)
					{
						_allowAnyConnetionType = allowAnyConnetionType;
						Logger.LogDebug(ToString());
					}

					return !_allowAnyConnetionType;
				}
			}

			public long MaxNonOnionConnectionCount { get; }

			public override string ToString()
			{
				return $"Connections: {ConnectedNodesCount}, Currently allow only onions: {!_allowAnyConnetionType}.";
			}
		}
	}
}
