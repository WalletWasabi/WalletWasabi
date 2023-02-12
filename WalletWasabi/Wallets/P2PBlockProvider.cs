using NBitcoin;
using NBitcoin.Protocol;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

/// <summary>
/// P2P block provider is a blocks provider getting the blocks from bitcoin nodes using the P2P bitcoin protocol.
/// </summary>
public class P2PBlockProvider : IBlockProvider
{
	public P2PBlockProvider(Network network, NodesGroup nodes, HttpClientFactory httpClientFactory)
	{
		P2PNodesManager = new P2PNodesManager(network, nodes, httpClientFactory.IsTorEnabled);
	}
	
	private P2PNodesManager P2PNodesManager { get; }

	/// <summary>
	/// Gets a bitcoin block from bitcoin nodes using the P2P bitcoin protocol.
	/// If a bitcoin node is available it fetches the blocks using the RPC interface.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		Block? block;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
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
					var timeout = P2PNodesManager.GetCurrentTimeout();
					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout)))
					{
						using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
						block = await node.DownloadBlockAsync(hash, lts.Token).ConfigureAwait(false);
					}

					// Validate block
					if (!block.Check())
					{
						P2PNodesManager.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.", force: true);
						continue;
					}

					P2PNodesManager.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}. Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");

					await P2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
				{
					await P2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);

					P2PNodesManager.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node
					continue;
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
					P2PNodesManager.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.", force: true);
					continue;
				}

				break; // If got this far, then we have the block and it's valid. Break.
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
		}

		return block;
	}
}
