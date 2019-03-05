using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi
{
	public class LocalNodeWrapper
	{
		private Node _node;
		private WasabiSynchronizer _synchronizer;

		public Node Internal => _node; 

		public static SyncBehavior SyncWatcher { get; } = new SyncBehavior();

		public EventHandler<EventArgs> FullySynchronized;

		public bool IsFullySynchronized { get; private set; }

		public LocalNodeWrapper(Node node)
		{
			_node = node;
			SyncWatcher.Synchronized += async (s, e)=>
				await CheckFullSynchronizationAsync();
		}

		public void Watch(WasabiSynchronizer synchronizer)
		{
			_synchronizer = synchronizer;
			_synchronizer.ResponseArrived += async (s, args) => {
				if( args.FiltersResponseState == FiltersResponseState.NoNewFilter )
				{
					await CheckFullSynchronizationAsync();
				}
				SyncWatcher.UpdateKnowTip(_synchronizer.BestKnownFilter.BlockHash);
			};
		}

		private async Task CheckFullSynchronizationAsync()
		{
			try
			{
				await Task.Run(()=>{
					var BlockHashes = new[] { _synchronizer.BestKnownFilter.BlockHash };
					var blocks = _node.GetBlocks(BlockHashes);
					if(blocks != null && blocks.Any(x=>x.GetHash() == BlockHashes[0]))
					{
						IsFullySynchronized = true;
						var syncEvent = FullySynchronized;
						if(syncEvent != null)
							syncEvent(this, EventArgs.Empty);
					}
				});
			}
			catch(Exception)
			{
				// TODO: handle this
			}
		}

		public static LocalNodeWrapper Connect(IPEndPoint endpoint, Network network, NodeConnectionParameters connectionParameters)
		{
			var handshakeTimeout = new CancellationTokenSource();
			handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(5));

			var localhost = new NetworkAddress(endpoint);
			var nodeConnectionParameters = connectionParameters.Clone();
			nodeConnectionParameters.ConnectCancellation = handshakeTimeout.Token;
			nodeConnectionParameters.IsRelay = true;
			
			try
			{
				var node = Node.Connect(network, endpoint, nodeConnectionParameters);
				Logger.LogInfo($"TCP Connection succeeded, handshaking...");
				node.VersionHandshake(Constants.LocalNodeRequirements, handshakeTimeout.Token);
				var peerServices = node.PeerVersion.Services;

				if(!peerServices.HasFlag(NodeServices.Network) && !peerServices.HasFlag(NodeServices.NODE_NETWORK_LIMITED))
				{
					throw new InvalidOperationException($"Wasabi cannot use the local node because it doesn't provide blocks.");
				}

				Logger.LogInfo($"Handshake completed successfully.");

				if (!node.IsConnected)
				{
					throw new InvalidOperationException($"Wasabi could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
						$"Probably this is because the node doesn't support retrieving full blocks or segwit serialization.");
				}
				node.Behaviors.Add(SyncWatcher);
				return new LocalNodeWrapper(node);
			}
			catch(Exception)
			{
				return null;
			}
		}
	}
}