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
			// Connect to nodes directly on clearnet
			ClearNet
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
				else if ( (ElapsedTimeSinceLastModeChange > TimeSpan.FromMinutes(1) && ConnectedNodesCount == 0)
						|| (ElapsedTimeSinceLastModeChange > TimeSpan.FromMinutes(2) && ConnectedNodesCount <= 3)
						|| (ElapsedTimeSinceLastModeChange > TimeSpan.FromMinutes(3) && ConnectedNodesCount <= 5))
				{
					Mode = Mode switch {
						ConnectionMode.OnionServiceOnly => ConnectionMode.AllowGoingThroughTorExitNodes,
						ConnectionMode.AllowGoingThroughTorExitNodes => ConnectionMode.ClearNet,
						ConnectionMode.ClearNet => ConnectionMode.ClearNet,
						_ => throw new ArgumentException($"Unknown {nameof(Mode)} value {Mode}.")
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
				var socksSettings = nodeConnectionParameters.TemplateBehaviors.Find<SocksSettingsBehavior>();

				switch (State.Mode)
				{
					case ConnectionMode.OnionServiceOnly:
						Connector.AllowOnlyTorEndpoints = true;  // throw if endPoint is not a Tor endpoint
						if (socksSettings is { })
						{
							socksSettings.OnlyForOnionHosts = false; // go through Tor always
						}
						break;
					case ConnectionMode.AllowGoingThroughTorExitNodes:
						Connector.AllowOnlyTorEndpoints = false; // do not throw if endPoint is not a Tor endpoint
						if (socksSettings is { })
						{
							socksSettings.OnlyForOnionHosts = false; // go through Tor always
						}
						break;
					case ConnectionMode.ClearNet:
						Connector.AllowOnlyTorEndpoints = false; // do not throw if endPoint is not a Tor endpoint
						if (socksSettings is { })
						{
							socksSettings.OnlyForOnionHosts = true; // go through Tor always
						}
						break;
				}
			}

			await Connector.ConnectSocket(socket, endPoint, nodeConnectionParameters, cancellationToken).ConfigureAwait(false);
			State.LastModeChangeTime = DateTimeOffset.UtcNow;
		}
	}
}
