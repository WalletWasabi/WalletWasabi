using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore
{
	public class P2pNode : IDisposable
	{
		private Node Node { get; set; }
		private TrustedP2pBehavior TrustedP2pBehavior { get; set; }
		public Network Network { get; }
		public EndPoint EndPoint { get; }
		public MempoolService MempoolService { get; }

		public event EventHandler<uint256> BlockInv;

		private bool NodeEventsSubscribed { get; set; }
		private object SubscriptionLock { get; }
		public AsyncLock ReconnectorLock { get; }
		private CancellationTokenSource Stop { get; set; }

		public P2pNode(Network network, EndPoint endPoint, MempoolService mempoolService, string userAgent)
		{
			Network = Guard.NotNull(nameof(network), network);
			EndPoint = Guard.NotNull(nameof(endPoint), endPoint);
			MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
			UserAgent = Guard.NotNullOrEmptyOrWhitespace(nameof(userAgent), userAgent, trim: true);

			Stop = new CancellationTokenSource();
			NodeEventsSubscribed = false;
			SubscriptionLock = new object();
			ReconnectorLock = new AsyncLock();
		}

		public async Task ConnectAsync(CancellationToken cancel)
		{
			using var handshakeTimeout = new CancellationTokenSource();
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(handshakeTimeout.Token, cancel, Stop.Token);
			handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(21));
			var parameters = new NodeConnectionParameters()
			{
				UserAgent = UserAgent,
				ConnectCancellation = linked.Token,
				IsRelay = true
			};

			parameters.TemplateBehaviors.Add(new TrustedP2pBehavior(MempoolService));

			Node = await Node.ConnectAsync(Network, EndPoint, parameters).ConfigureAwait(false);
			Node.VersionHandshake();

			if (!Node.PeerVersion.Services.HasFlag(NodeServices.Network))
			{
				throw new InvalidOperationException("Wasabi cannot use the local node because it does not provide blocks.");
			}

			if (!Node.IsConnected)
			{
				throw new InvalidOperationException(
					$"Could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
					"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
			}

			lock (SubscriptionLock)
			{
				TrustedP2pBehavior = Node.Behaviors.Find<TrustedP2pBehavior>();
				Node.UncaughtException += Node_UncaughtException;
				Node.StateChanged += P2pNode_StateChanged;
				Node.Disconnected += Node_DisconnectedAsync;
				TrustedP2pBehavior.BlockInv += TrustedP2pBehavior_BlockInv;
				NodeEventsSubscribed = true;
				MempoolService.TrustedNodeMode = Node.IsConnected;
			}
		}

		private void Node_UncaughtException(Node sender, Exception ex)
		{
			Logger.LogInfo($"Node {sender.Peer.Endpoint} failed with exception: {ex}");
		}

		private async void Node_DisconnectedAsync(Node node)
		{
			try
			{
				using (await ReconnectorLock.LockAsync(Stop.Token).ConfigureAwait(false))
				{
					if (node.IsConnected)
					{
						return;
					}
					var reconnector = new P2pReconnector(TimeSpan.FromSeconds(7), this);
					await reconnector.StartAndAwaitReconnectionAsync(Stop.Token).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
		}

		private void TrustedP2pBehavior_BlockInv(object sender, uint256 e)
		{
			BlockInv?.Invoke(this, e);
		}

		public string UserAgent { get; }

		private void P2pNode_StateChanged(Node node, NodeState oldState)
		{
			var isConnected = node.IsConnected;
			var trustedNodeMode = MempoolService.TrustedNodeMode;
			if (trustedNodeMode != isConnected)
			{
				MempoolService.TrustedNodeMode = isConnected;
				Logger.LogInfo($"CoreNode connection state changed. Triggered {nameof(MempoolService)}.{nameof(MempoolService.TrustedNodeMode)} to be {MempoolService.TrustedNodeMode}");
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Stop?.Cancel();
					Disconnect();
					Stop?.Dispose();
					Stop = null;
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		/// <summary>
		/// It is not equivalent to Dispose, but it is being called from Dispose.
		/// </summary>
		public void Disconnect()
		{
			Node node = Node;
			if (node is { })
			{
				lock (SubscriptionLock)
				{
					MempoolService.TrustedNodeMode = false;
					if (NodeEventsSubscribed)
					{
						var trustedP2pBehavior = TrustedP2pBehavior;
						if (trustedP2pBehavior is { })
						{
							trustedP2pBehavior.BlockInv -= TrustedP2pBehavior_BlockInv;
						}
						node.Disconnected -= Node_DisconnectedAsync;
						node.StateChanged -= P2pNode_StateChanged;
						node.UncaughtException -= Node_UncaughtException;
						NodeEventsSubscribed = false;
					}
				}

				try
				{
					node.Disconnect();
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
				finally
				{
					try
					{
						node.Dispose();
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
					finally
					{
						Node = null;
						Logger.LogInfo("P2p Bitcoin node is disconnected.");
					}
				}
			}
		}

		#endregion IDisposable Support
	}
}
