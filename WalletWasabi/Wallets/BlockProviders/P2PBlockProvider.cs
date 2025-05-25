using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets.BlockProviders;

public class P2PBlockProvider : IBlockProvider
{
	public P2PBlockProvider(P2PNodesManager p2PNodesManager)
	{
		_p2PNodesManager = p2PNodesManager;
	}

	internal P2PBlockProvider(Network network, NodesGroup nodes)
		: this(new P2PNodesManager(network, nodes))
	{
	}

	private readonly P2PNodesManager _p2PNodesManager;

	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var node = await _p2PNodesManager.GetNodeForSingleUseAsync(cancellationToken).ConfigureAwait(false);

			double timeout = _p2PNodesManager.GetCurrentTimeout();

			// Download block from the selected node.
			try
			{
				Block? block;

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout)))
				{
					using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
					block = await node.DownloadBlockAsync(blockHash, lts.Token).ConfigureAwait(false);
				}

				// Validate block
				if (!block.Check())
				{
					_p2PNodesManager.DisconnectNode(node,
						$"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.");

					continue;
				}

				Logger.LogInfo($"Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");
				_p2PNodesManager.DisconnectNodeIfEnoughPeers(node,
					$"Disconnected node: {node.RemoteSocketAddress}. Block downloaded.");

				await _p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);

				return block;
			}
			catch (Exception ex)
			{
				if (ex is OperationCanceledException or TimeoutException)
				{
					await _p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);
					_p2PNodesManager.DisconnectNodeIfEnoughPeers(node,
						$"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node
				}
				else
				{
					Logger.LogDebug(ex);
					_p2PNodesManager.DisconnectNode(node,
						$"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.");
				}
			}
		}
		cancellationToken.ThrowIfCancellationRequested();
		throw new InvalidOperationException("Failed to retrieve the requested block.");
	}
}
