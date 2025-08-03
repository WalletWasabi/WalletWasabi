using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
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
				var nodesCollection = await p2PNodesManager.GetNodeForSingleUseAsync(cancellationToken).ConfigureAwait(false);
				var availableNodes = nodesCollection.ToArray();

				if (availableNodes.Length == 0)
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
				}

				double timeout = p2PNodesManager.GetCurrentTimeout();

				// Create download tasks for all nodes simultaneously
				var downloadTasks = new List<Task<(Node node, Block? block, Exception? exception)>>();
				using var globalCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, cancellationToken);

				foreach (var node in availableNodes)
				{
					var downloadTask = DownloadBlockFromNodeAsync(node, blockHash, linkedCts.Token);
					downloadTasks.Add(downloadTask);
				}

				try
				{
					// Wait for the first successful download
					var completedTask = await Task.WhenAny(downloadTasks).ConfigureAwait(false);
					var (winnerNode, block, exception) = await completedTask.ConfigureAwait(false);

					// Cancel all other downloads
					linkedCts.Cancel();

					// Wait a bit for other tasks to cancel gracefully
					try
					{
						await Task.WhenAll(downloadTasks).ConfigureAwait(false);
					}
					catch
					{
						// Ignore cancellation exceptions from other tasks
					}

					if (block is not null && block.Check())
					{
						Logger.LogInfo($"Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");

						// Randomly disconnect one of the slower nodes
						var slowNodes = availableNodes.Where(n => n != winnerNode).ToArray();
						if (slowNodes.Length > 0)
						{
							var random = new Random();
							var nodeToDisconnect = slowNodes[random.Next(slowNodes.Length)];
							p2PNodesManager.DisconnectNode(nodeToDisconnect,
								$"Disconnected node: {nodeToDisconnect.RemoteSocketAddress}, because it was slower than the winner.");
						}

						await p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: false).ConfigureAwait(false);
						return block;
					}
					if (block is not null && !block.Check())
					{
						p2PNodesManager.DisconnectNode(winnerNode,
							$"Disconnected node: {winnerNode.RemoteSocketAddress}, because invalid block received.");
					}
					else if (exception is not null)
					{
						if (exception is OperationCanceledException or TimeoutException)
						{
							await p2PNodesManager.UpdateTimeoutAsync(increaseDecrease: true).ConfigureAwait(false);
							p2PNodesManager.DisconnectNodeIfEnoughPeers(winnerNode,
								$"Disconnected node: {winnerNode.RemoteSocketAddress}, because block download took too long.");
						}
						else
						{
							Logger.LogDebug(exception);
							p2PNodesManager.DisconnectNode(winnerNode,
								$"Disconnected node: {winnerNode.RemoteSocketAddress}, because block download failed: {exception.Message}.");
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
					// If all tasks failed, try again
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			throw new InvalidOperationException("Failed to retrieve the requested block.");
		};

	private static async Task<(Node node, Block? block, Exception? exception)> DownloadBlockFromNodeAsync(Node node, uint256 blockHash, CancellationToken cancellationToken)
	{
		try
		{
			var block = await node.DownloadBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
			return (node, block, null);
		}
		catch (Exception ex)
		{
			return (node, null, ex);
		}
	}

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
