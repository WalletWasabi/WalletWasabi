using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets.BlockProvider;
using DownloadResult = WalletWasabi.Helpers.Result<NBitcoin.Block,WalletWasabi.Wallets.FilterProcessor.DownloadError>;

namespace WalletWasabi.Wallets.FilterProcessor;

/// <summary>
/// Service that prioritizes and handles block downloads.
/// </summary>
public class BlockDownloadService : BackgroundService
{
	/// <summary>Maximum number of parallel block-downloading tasks.</summary>
	private const int MaxParallelTasks = 5;

	public BlockDownloadService(
		IFileSystemBlockRepository fileSystemBlockRepository,
		IBlockProvider[] trustedFullNodeBlockProviders,
		IBlockProvider? p2pBlockProvider,
		int maximumParallelTasks = MaxParallelTasks)
	{
		_fileSystemBlockRepository = fileSystemBlockRepository;
		_trustedFullNodeBlockProviders = trustedFullNodeBlockProviders;
		_p2PBlockProvider = p2pBlockProvider;
		MaximumParallelTasks = maximumParallelTasks;
	}

	private readonly IFileSystemBlockRepository _fileSystemBlockRepository;
	private readonly IBlockProvider[] _trustedFullNodeBlockProviders;

	/// <remarks><c>null</c> means that no P2P provider is available.</remarks>
	private readonly IBlockProvider? _p2PBlockProvider;
	public int MaximumParallelTasks { get; }

	/// <summary>Signals that there is a block-download request or multiple block-download requests.</summary>
	private readonly SemaphoreSlim _requestAvailableSemaphore = new(initialCount: 0, maxCount: 1);

	/// <summary>Block hashes that are to be downloaded. Block height represents priority of the priority queue.</summary>
	/// <remarks>
	/// Guarded by <see cref="_lock"/>.
	/// <para>Internal for testing purposes.</para>
	/// </remarks>
	internal PriorityQueue<Request, uint> BlocksToDownloadRequests { get; } = new();

	/// <remarks>Guards <see cref="BlocksToDownloadRequests"/>.</remarks>
	private readonly object _lock = new();

	/// <summary>
	/// Attempts to get given block from the given source. It can take a long time to get the result because priority of block download is taken into account.
	/// </summary>
	/// <returns>One of the following result objects:
	/// <list type="bullet">
	/// <item><see cref="SuccessResult"/> when the block was downloaded successfully.</item>
	/// <item><see cref="CanceledResult"/> when cancelled using the cancellation token or if the service is shutting down.</item>
	/// <item><see cref="FailureResult"/> when the block download failed for some reason.</item>
	/// </list>
	/// </returns>
	/// <remarks>The method does not throw exceptions.</remarks>
	public async Task<DownloadResult> TryGetBlockAsync(BlockSource source, uint256 blockHash, uint blockHeight, CancellationToken cancellationToken)
	{
		Request request = new(source, blockHash, blockHeight, new TaskCompletionSource<DownloadResult>());
		Enqueue(request);

		try
		{
			await request.Tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			request.Tcs.TrySetResult(DownloadResult.Fail(DownloadError.Canceled));
		}

		// Now the task is guaranteed to return a result.
		return await request.Tcs.Task.ConfigureAwait(false);
	}

	private void Enqueue(Request request)
	{
		lock (_lock)
		{
			int count = BlocksToDownloadRequests.Count;
			BlocksToDownloadRequests.Enqueue(request, request.BlockHeight);

			if (count == 0 && _requestAvailableSemaphore.CurrentCount == 0)
			{
				_requestAvailableSemaphore.Release();
			}
		}
	}

	/// <summary>
	/// Remove all blocks whose height is larger or equal to <paramref name="maxBlockHeight"/>.
	/// </summary>
	/// <remarks>
	/// The method is not efficient but it does not matter too much because the operation is not supposed to be called often.
	/// The main use case is to deal with a blockchain reorg.
	/// </remarks>
	public async Task RemoveBlocksAsync(uint maxBlockHeight)
	{
		PriorityQueue<Request, uint> tempQueue = new();

		List<uint256> toRemoveFromCache = [];

		lock (_lock)
		{
			var items = BlocksToDownloadRequests.UnorderedItems.ToList();

			foreach (var (request, blockHeight) in items)
			{
				if (blockHeight <= maxBlockHeight)
				{
					tempQueue.Enqueue(request, blockHeight);
				}
				else
				{
					// The block might have been downloaded by now so just try to set the result.
					request.Tcs.TrySetResult(DownloadResult.Fail(DownloadError.ReorgOccurred));
					toRemoveFromCache.Add(request.BlockHash);
				}
			}

			BlocksToDownloadRequests.Clear();
			BlocksToDownloadRequests.EnqueueRange(tempQueue.UnorderedItems);
		}

		foreach (uint256 blockHash in toRemoveFromCache)
		{
			await _fileSystemBlockRepository.RemoveAsync(blockHash, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Downloads blocks in parallel if there are blocks to download.
	/// </summary>
	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			List<Task> activeTasks = new(capacity: MaximumParallelTasks);

			while (!cancellationToken.IsCancellationRequested)
			{
				bool wait;

				lock (_lock)
				{
					// Wait until at least one block-downloading request is here; otherwise, consume requests so that there is at most MAX parallel tasks.
					wait = BlocksToDownloadRequests.Count == 0;
				}

				if (wait)
				{
					await _requestAvailableSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
				}

				lock (_lock)
				{
					int toStart = Math.Min(BlocksToDownloadRequests.Count, MaximumParallelTasks - activeTasks.Count);

					for (int i = 0; i < toStart; i++)
					{
						if (!BlocksToDownloadRequests.TryDequeue(out Request? queuedRequest, out _))
						{
							throw new UnreachableException("Failed to dequeue block from the queue.");
						}

						Task newTask = HandleSingleBlockTaskAsync(queuedRequest, cancellationToken);
						activeTasks.Add(newTask);
					}
				}

				// It's still possible that there is no task because a reorg might have occurred.
				if (activeTasks.Count == 0)
				{
					continue;
				}

				if (activeTasks.Count == MaximumParallelTasks)
				{
					Task task = await Task.WhenAny(activeTasks).ConfigureAwait(false);
					activeTasks.Remove(task);
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Block-downloading service execution was stopped.");
		}
		catch (Exception ex)
		{
			// This shouldn't happen.
			Logger.LogError(ex);
			TerminateService.Instance?.SignalGracefulCrash(ex);
			throw;
		}
		finally
		{
			// Mark everything as cancelled because the service is shutting down (either gracefully or forcibly).
			lock (_lock)
			{
				while (BlocksToDownloadRequests.TryDequeue(out Request? request, out _))
				{
					request.Tcs.TrySetResult(DownloadResult.Fail(DownloadError.Canceled));
				}
			}
		}
	}

	private async Task HandleSingleBlockTaskAsync(Request request, CancellationToken cancellationToken)
	{
		var response = await DownloadSingleBlockAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.IsOk)
		{
			request.Tcs.TrySetResult(response);
		}
		else
		{
			Logger.LogDebug($"Attempt to download block {request.BlockHash} (height: {request.BlockHeight}) failed.");

			// The block might have been removed concurrently if a reorg occurred.
			if (!request.Tcs.TrySetResult(response))
			{
				var opResult = await request.Tcs.Task.ConfigureAwait(false);
				Logger.LogDebug($"Failed to set result for block '{request.BlockHash}' (height: {request.BlockHeight}). Result is already: {opResult}");
			}
		}
	}

	/// <summary>
	/// Downloads a single block from a block source, or gets the block from the file-system cache if available.
	/// </summary>
	private async Task<DownloadResult> DownloadSingleBlockAsync(Request request, CancellationToken cancellationToken)
	{
		Logger.LogTrace($"Trying to download {request.BlockHash} (height: {request.BlockHeight}).");

		try
		{
			// Try to get the block from the file-system storage.
			Block? block = await _fileSystemBlockRepository.TryGetAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);
			if (block is not null)
			{
				return block;
			}

			if (request.BlockSource == BlockSource.TrustedNode)
			{
				// Try to get the block from a trusted node, whether it's integrated or distant.
				if (_trustedFullNodeBlockProviders.Length == 0)
				{
					return DownloadResult.Fail(DownloadError.NoSuchProvider);
				}

				foreach (IBlockProvider blockProvider in _trustedFullNodeBlockProviders)
				{
					block = await blockProvider.TryGetBlockAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);

					if (block is not null)
					{
						break;
					}
				}
			}
			else if (request.BlockSource == BlockSource.P2pNetwork)
			{
				// Try to get the block from the P2P Network.
				if (_p2PBlockProvider is null)
				{
					return DownloadResult.Fail(DownloadError.NoSuchProvider);
				}

				block = await _p2PBlockProvider.TryGetBlockAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);
			}

			if(block is null)
			{
				return DownloadResult.Fail(DownloadError.Failure);
			}

			// Store the block to the file-system.
			try
			{
				await _fileSystemBlockRepository.SaveAsync(block, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Failed to cache block {request.BlockHash} (height: {request.BlockHeight})", ex);
			}
			return block;
		}
		catch (OperationCanceledException)
		{
			return DownloadResult.Fail(DownloadError.Canceled);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Exception thrown while getting block {request.BlockHash} (height: {request.BlockHeight})", ex);
			throw new UnreachableException("Unexpected exception occurred", ex);
		}
	}

	/// <param name="Tcs">By design, this task completion source is not supposed to be ended by </param>
	internal record Request(BlockSource BlockSource, uint256 BlockHash, uint BlockHeight, TaskCompletionSource<DownloadResult> Tcs);
}
