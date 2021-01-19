using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi
{
	public class BestEffortEndpointConnector : IEnpointConnector
	{
		public enum ConnectionMode
		{
			// Connect to nodes published as onion services only
			OnionServiceOnly,
			// Connect to nodes published on the clearnet through Tor exit nodes
			AllowGoingThroughTorExitNodes,
		}

		// A class to share state between all the BestEffortEndpointConnector connectors.
		// This is necessary because NBitcoin clones the connectors for every new node connection
		// attempt using the original connector.
		public class EffortState
		{
			private long _connectedNodesCount;
			private DateTimeOffset _lastModeChangeTime;
			private bool _lostConnection;
			public long ConnectedNodesCount
			{
				get => _connectedNodesCount;
				set
				{
					_lostConnection = value == 0 && _connectedNodesCount > 0;
					_connectedNodesCount = value;
				}
			}

			public ConnectionMode Mode { get; set; }
			public DateTimeOffset LastModeChangeTime
			{
				get => _lastModeChangeTime;
				set
				{
					_lostConnection = false;
					_lastModeChangeTime = value;
				}
			}

			public TimeSpan ElapsedTimeSinceLastModeChange => DateTimeOffset.UtcNow - LastModeChangeTime;

			public EffortState(ConnectionMode mode, DateTimeOffset lastModeChangeTime)
			{
				Mode = mode;
				LastModeChangeTime = lastModeChangeTime;
			}

			public bool CheckModeUpdate()
			{
				var previousMode = Mode;
				if (_lostConnection)
				{
					Mode = ConnectionMode.OnionServiceOnly;
				}
				else if ( ElapsedTimeSinceLastModeChange > TimeSpan.FromMinutes(1))
				{
					if (Mode == ConnectionMode.OnionServiceOnly)
					{
						Mode = ConnectionMode.AllowGoingThroughTorExitNodes;
					};
				}
				if (previousMode != Mode)
				{
					Logger.LogInfo($"Update connection mode from {previousMode} to {Mode}.");
					LastModeChangeTime = DateTimeOffset.UtcNow;
					return true;
				}
				return false;
			}
		}

		public DefaultEndpointConnector Connector { get; private set; }
		public EffortState State { get; private set; }

		public BestEffortEndpointConnector()
			: this(
				new DefaultEndpointConnector(allowOnlyTorEndpoints: true),
				new EffortState(ConnectionMode.OnionServiceOnly, DateTimeOffset.UtcNow))
		{
		}

		private BestEffortEndpointConnector(DefaultEndpointConnector connector, EffortState state)
		{
			Connector = connector;
			State = state;
		}

		public void UpdateConnectedNodesCounter(int connectedNodes)
		{
			State.ConnectedNodesCount = connectedNodes;
		}

		public IEnpointConnector Clone()
		{
			return new BestEffortEndpointConnector(Connector, State);
		}

		public virtual async Task ConnectSocket(Socket socket, EndPoint endPoint, NodeConnectionParameters nodeConnectionParameters, CancellationToken cancellationToken)
		{
			if (State.CheckModeUpdate())
			{
				Connector.AllowOnlyTorEndpoints = State.Mode switch
				{
					ConnectionMode.OnionServiceOnly => true,
					ConnectionMode.AllowGoingThroughTorExitNodes => false,
					_ => throw new InvalidOperationException($"Unknown ${typeof(ConnectionMode).Name} with value {State.Mode}.")
				};
			}

			await Connector.ConnectSocket(socket, endPoint, nodeConnectionParameters, cancellationToken).ConfigureAwait(false);
			State.LastModeChangeTime = DateTimeOffset.UtcNow;
		}
	}
}
