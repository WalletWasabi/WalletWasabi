using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.NodesManagement;

namespace WalletWasabi.Wallets;

public delegate Task<Block?> BlockProvider(uint256 blockHash, CancellationToken cancellationToken);

public static class BlockProviders
{
	public static BlockProvider FileSystemBlockProvider(FileSystemBlockRepository fs) =>
		fs.TryGetBlockAsync;

	public static BlockProvider RpcBlockProvider(IRPCClient rpcClient) =>
		async (blockHash, cancellationToken) =>
		{
			try
			{
				return await rpcClient.GetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"RPC block provider failed to retrieve block {blockHash}: {ex}");
				return null;
			}
		};

	public static BlockProvider P2pBlockProvider(INodesRegistry nodesRegistry, EventBus eventBus) =>
		async (blockHash, cancellationToken) =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var node = await nodesRegistry.GetNodeForSingleUseAsync(cancellationToken).ConfigureAwait(false);

				double timeout = nodesRegistry.GetCurrentTimeout();

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
						nodesRegistry.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.");
						eventBus.Publish(new MisbehavingNodeDetected(node.RemoteSocketEndpoint, node));
						continue;
					}

					Logger.LogInfo($"Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");
					nodesRegistry.UpdateTimeout(increaseDecrease: false);

					return block;
				}
				catch (Exception ex)
				{
					if (ex is OperationCanceledException or TimeoutException)
					{
						nodesRegistry.UpdateTimeout(increaseDecrease: true);

						// It could be a slow connection and not a misbehaving node.
						nodesRegistry.DisconnectNodeIfEnoughPeers(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download took too long.");
						eventBus.Publish(new NodeTimeoutDownloadingBlock(node.RemoteSocketEndpoint, node));
					}
					else
					{
						Logger.LogDebug(ex);
						nodesRegistry.DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.");
					}
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
			throw new InvalidOperationException("Failed to retrieve the requested block.");
		};

	public static BlockProvider ComposedBlockProvider(BlockProvider[] blockProviders) =>
		async (blockHash, cancellationToken) =>
		{
			foreach (var blockProvider in blockProviders)
			{
				var block = await blockProvider(blockHash, cancellationToken).ConfigureAwait(false);

				if (block is not null)
				{
					return block;
				}
			}

			return null;
		};

	public static BlockProvider CachedBlockProvider(BlockProvider blockProvider, FileSystemBlockRepository fs) =>
		async (blockHash, cancellationToken) =>
		{
			var block = await blockProvider(blockHash, cancellationToken).ConfigureAwait(false);
			if (block is not null)
			{
				await fs.SaveAsync(block, cancellationToken).ConfigureAwait(false);
			}

			return block;
		};
}
