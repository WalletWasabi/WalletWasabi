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
	public class P2pNode
	{
		private Node Node { get; set; }
		private TrustedP2pBehavior TrustedP2pBehavior { get; set; }
		public Network Network { get; }
		public EndPoint EndPoint { get; }
		public MempoolService MempoolService { get; }

		public event EventHandler<uint256> BlockInv;

		private bool NodeEventsSubscribed { get; set; }
		private object SubscriptionLock { get; }

		private CancellationTokenSource Stop { get; set; }
		private P2pReconnector Reconnector { get; set; }
		private AsyncLock ReconnectorLock { get; }

		public P2pNode(Network network, EndPoint endPoint, MempoolService mempoolService, string userAgent)
		{
			Network = Guard.NotNull(nameof(network), network);
			EndPoint = Guard.NotNull(nameof(endPoint), endPoint);
			MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
			UserAgent = Guard.NotNullOrEmptyOrWhitespace(nameof(userAgent), userAgent, trim: true);

			Stop = new CancellationTokenSource();
			NodeEventsSubscribed = false;
			SubscriptionLock = new object();
			Reconnector = null;
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
				Node.StateChanged += P2pNode_StateChanged;
				Node.Disconnected += Node_DisconnectedAsync;
				TrustedP2pBehavior.BlockInv += TrustedP2pBehavior_BlockInv;
				NodeEventsSubscribed = true;
				MempoolService.TrustedNodeMode = Node.IsConnected;
			}
		}

		private async void Node_DisconnectedAsync(Node node)
		{
			try
			{
				using (await ReconnectorLock.LockAsync(Stop.Token).ConfigureAwait(false))
				{
					if (Reconnector is { })
					{
						return;
					}

					Reconnector = new P2pReconnector(TimeSpan.FromSeconds(7), this);
					Reconnector.PropertyChanged += Reconnector_PropertyChangedAsync;
					Reconnector.Start();
				}
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
		}

		private async void Reconnector_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
		{
			try
			{
				using (await ReconnectorLock.LockAsync(Stop.Token).ConfigureAwait(false))
				{
					if (e.PropertyName == nameof(Reconnector.Status) && Reconnector.Status)
					{
						Reconnector.PropertyChanged -= Reconnector_PropertyChangedAsync;
						await Reconnector.StopAsync().ConfigureAwait(false);
						Reconnector = null;
					}
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

		public async Task DisposeAsync()
		{
			Stop?.Cancel();
			using (await ReconnectorLock.LockAsync().ConfigureAwait(false))
			{
				if (Reconnector is { })
				{
					Reconnector.PropertyChanged -= Reconnector_PropertyChangedAsync;
					await Reconnector.StopAsync().ConfigureAwait(false);
					Reconnector = null;
				}
			}
			Disconnect();
			Stop?.Dispose();
			Stop = null;
		}

		/// <summary>
		/// It is not equivalent to Dispose, but it is being called from Dispose.
		/// </summary>
		public void Disconnect()
		{
			lock (SubscriptionLock)
			{
				MempoolService.TrustedNodeMode = false;
				if (NodeEventsSubscribed)
				{
					TrustedP2pBehavior.BlockInv -= TrustedP2pBehavior_BlockInv;
					Node.Disconnected -= Node_DisconnectedAsync;
					Node.StateChanged -= P2pNode_StateChanged;
					NodeEventsSubscribed = false;
				}
			}

			if (Node != null)
			{
				try
				{
					Node?.Disconnect();
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
				finally
				{
					try
					{
						Node?.Dispose();
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
	}
}
