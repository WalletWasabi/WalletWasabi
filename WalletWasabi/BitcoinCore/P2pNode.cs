using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

		public P2pNode(Network network, EndPoint endPoint, MempoolService mempoolService, string userAgent)
		{
			Network = Guard.NotNull(nameof(network), network);
			EndPoint = Guard.NotNull(nameof(endPoint), endPoint);
			MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
			UserAgent = Guard.NotNullOrEmptyOrWhitespace(nameof(userAgent), userAgent, trim: true);
		}

		public async Task ConnectAsync(CancellationToken cancel)
		{
			using var handshakeTimeout = new CancellationTokenSource();
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(handshakeTimeout.Token, cancel);
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

			Node.StateChanged += P2pNode_StateChanged;
			P2pNodeStateChangedSubscribed = true;
			MempoolService.TrustedNodeMode = Node.IsConnected;

			TrustedP2pBehavior = Node.Behaviors.Find<TrustedP2pBehavior>();
			TrustedP2pBehavior.BlockInv += TrustedP2pBehavior_BlockInv;
			TrustedP2pBehaviorBlockInvSubscribed = true;
		}

		private bool TrustedP2pBehaviorBlockInvSubscribed { get; set; }

		private void TrustedP2pBehavior_BlockInv(object sender, uint256 e)
		{
			BlockInv?.Invoke(this, e);
		}

		private bool P2pNodeStateChangedSubscribed { get; set; }
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
					if (TrustedP2pBehaviorBlockInvSubscribed)
					{
						TrustedP2pBehavior.BlockInv -= TrustedP2pBehavior_BlockInv;
					}
					if (P2pNodeStateChangedSubscribed)
					{
						Node.StateChanged -= P2pNode_StateChanged;
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

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
