using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Logging;
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

	public static BlockProvider P2pBlockProvider(P2pNodeProvider getNode) =>
		async (blockHash, cancellationToken) =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var singleUseNodeGroup = await getNode(cancellationToken).ConfigureAwait(false);
				var block = await singleUseNodeGroup.GetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
				if (block is not null)
				{
					return block;
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
