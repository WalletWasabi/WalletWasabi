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
		P2PNodesManager = p2PNodesManager;
	}

	/// <remarks>For tests only.</remarks>
	public P2PBlockProvider(Network network, NodesGroup nodes, bool isTorEnabled)
		: this(new P2PNodesManager(network, nodes, isTorEnabled))
	{
	}

	private P2PNodesManager P2PNodesManager { get; }

	/// <summary>
	/// Gets the given block from a single P2P node using the P2P bitcoin protocol.
	/// </summary>
	/// <param name="blockHash">Block's hash to download.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <returns>Requested block, or <c>null</c> if the block could not get downloaded for any reason.</returns>
	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		P2pBlockResponse? blockWithSourceData = await TryGetBlockWithSourceDataAsync(blockHash, cancellationToken).ConfigureAwait(false);
		return blockWithSourceData?.Block;
	}

	/// <inheritdoc/>
	public async Task<P2pBlockResponse> TryGetBlockWithSourceDataAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		Node? node;
		uint connectedNodes;

		try
		{
			(node, connectedNodes) = await P2PNodesManager.GetNodeAsync(cancellationToken).ConfigureAwait(false);

			if (node is null || !node.IsConnected)
			{
				return new P2pBlockResponse(Block: null, new P2pSourceData(P2pSourceDataCode.NoPeerAvailable, Node: null, connectedNodes));
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}

		// Download block from the selected node.
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

				return new P2pBlockResponse(Block: null, new P2pSourceData(P2pSourceDataCode.InvalidBlockProvided, node, connectedNodes));
			}

			P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}. Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");

			await P2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);

			return new P2pBlockResponse(block, new P2pSourceData(P2pSourceDataCode.OK, node, connectedNodes));
		}
		catch (Exception ex)
		{
			if (ex is OperationCanceledException or TimeoutException)
			{
				await P2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);
				P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node

				return new P2pBlockResponse(Block: null, new P2pSourceData(P2pSourceDataCode.Cancelled, node, connectedNodes));
			}
			else
			{
				Logger.LogDebug(ex);
				P2PNodesManager.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.", force: true);

				return new P2pBlockResponse(Block: null, new P2pSourceData(P2pSourceDataCode.Failure, node, connectedNodes));
			}
		}
	}
}
