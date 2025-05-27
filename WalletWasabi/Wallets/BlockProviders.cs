using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

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
				Logger.LogDebug(ex);
				return null;
			}
		};

	public static BlockProvider P2pBlockProvider(P2PNodesManager p2PNodesManager) =>
		async (blockHash, cancellationToken) =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var node = await p2PNodesManager.GetNodeForSingleUseAsync(cancellationToken).ConfigureAwait(false);

				double timeout = p2PNodesManager.GetCurrentTimeout();

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
						p2PNodesManager.DisconnectNode(node,
							$"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.");

						continue;
					}

					Logger.LogInfo($"Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");
					p2PNodesManager.DisconnectNodeIfEnoughPeers(node,
						$"Disconnected node: {node.RemoteSocketAddress}. Block downloaded.");

					await p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);

					return block;
				}
				catch (Exception ex)
				{
					if (ex is OperationCanceledException or TimeoutException)
					{
						await p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);
						p2PNodesManager.DisconnectNodeIfEnoughPeers(node,
							$"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node
					}
					else
					{
						Logger.LogDebug(ex);
						p2PNodesManager.DisconnectNode(node,
							$"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.");
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

	public static BlockProvider CachedBlockProvider(BlockProvider blockProvider, IFileSystemBlockRepository fs) =>
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
