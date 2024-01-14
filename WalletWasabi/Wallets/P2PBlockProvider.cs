using NBitcoin;
using NBitcoin.Protocol;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

/// <summary>
/// P2P block provider is a blocks provider getting the blocks from bitcoin nodes using the P2P bitcoin protocol.
/// </summary>
public class P2PBlockProvider : IBlockProvider
{
	public P2PBlockProvider(Network network, NodesGroup nodes, bool isTorEnabled)
	{
		P2PNodesManager = new P2PNodesManager(network, nodes, isTorEnabled);
	}

	private P2PNodesManager P2PNodesManager { get; }

	/// <summary>
	/// Gets a bitcoin block from bitcoin nodes using the P2P bitcoin protocol.
	/// If a bitcoin node is available it fetches the blocks using the RPC interface.
	/// </summary>
	/// <param name="blockHash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		while (true)
		{
			try
			{
				Node? node = await P2PNodesManager.GetNodeAsync(cancellationToken).ConfigureAwait(false);

				if (node is null || !node.IsConnected)
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
					continue;
				}

				// Download block from selected node.
				try
				{
					Block? block;
					var timeout = P2PNodesManager.GetCurrentTimeout();
					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout)))
					{
						using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
						block = await node.DownloadBlockAsync(blockHash, lts.Token).ConfigureAwait(false);
					}

					// Validate block
					if (!block.Check())
					{
						P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.", force: true);
						continue;
					}

					P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}. Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");

					await P2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);

					return block;
				}
				catch (Exception ex)
				{
					if (ex is OperationCanceledException or TimeoutException)
					{
						await P2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);
						P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node
					}
					else
					{
						Logger.LogDebug(ex);
						P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.", force: true);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
		}
	}
}
