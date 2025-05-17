using NBitcoin;
using NBitcoin.Protocol;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets.BlockProvider;

namespace WalletWasabi.Wallets;

/// <summary>
/// P2P block provider is a blocks provider getting the blocks from bitcoin nodes using the P2P bitcoin protocol.
/// </summary>
public class P2PBlockProvider : IP2PBlockProvider
{
	public P2PBlockProvider(P2PNodesManager p2PNodesManager)
	{
		_p2PNodesManager = p2PNodesManager;
	}

	/// <remarks>For tests only.</remarks>
	internal P2PBlockProvider(Network network, NodesGroup nodes)
		: this(new P2PNodesManager(network, nodes))
	{
	}

	private readonly P2PNodesManager _p2PNodesManager;

	/// <summary>
	/// Gets the given block from a single, automatically selected, P2P node using the P2P bitcoin protocol.
	/// </summary>
	/// <param name="blockHash">Block's hash to download.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <returns>Requested block, or <c>null</c> if the block could not get downloaded for any reason.</returns>
	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		P2pBlockResponse blockWithSourceData = await TryGetBlockWithSourceDataAsync(blockHash, P2pSourceRequest.Automatic, cancellationToken).ConfigureAwait(false);
		return blockWithSourceData.Block;
	}

	/// <inheritdoc/>
	public async Task<P2pBlockResponse> TryGetBlockWithSourceDataAsync(uint256 blockHash, P2pSourceRequest sourceRequest, CancellationToken cancellationToken)
	{
		Node? node = sourceRequest.Node;

		if (node is null)
		{
			node = await _p2PNodesManager.GetNodeAsync(cancellationToken).ConfigureAwait(false);

			if (node is null || !node.IsConnected)
			{
				return new P2pBlockResponse(Block: null);
			}
		}

		double timeout = sourceRequest.Timeout ?? _p2PNodesManager.GetCurrentTimeout();

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
				_p2PNodesManager.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.");

				return new P2pBlockResponse(Block: null);
			}

			_p2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}. Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");

			await _p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);

			return new P2pBlockResponse(block);
		}
		catch (Exception ex)
		{
			if (ex is OperationCanceledException or TimeoutException)
			{
				await _p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);
				_p2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node

				return new P2pBlockResponse(Block: null);
			}
			else
			{
				Logger.LogDebug(ex);
				_p2PNodesManager.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.");

				return new P2pBlockResponse(Block: null);
			}
		}
	}
}
