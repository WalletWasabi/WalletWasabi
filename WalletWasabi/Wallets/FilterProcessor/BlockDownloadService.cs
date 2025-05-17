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
		IP2PBlockProvider? p2pBlockProvider,
		int maximumParallelTasks = MaxParallelTasks)
	{
		_fileSystemBlockRepository = fileSystemBlockRepository;
		_trustedFullNodeBlockProviders = trustedFullNodeBlockProviders;
		_p2PBlockProvider = p2pBlockProvider;
		MaximumParallelTasks = maximumParallelTasks;
	}

	/// <summary>Result object describing if/how object was downloaded using the block downloading service.</summary>
	public interface IResult;

	/// <summary>Denotes a failure of getting a block.</summary>
	public interface IFailureResult : IResult;

	private readonly IFileSystemBlockRepository _fileSystemBlockRepository;
	private readonly IBlockProvider[] _trustedFullNodeBlockProviders;

	/// <remarks><c>null</c> means that no P2P provider is available.</remarks>
	private readonly IP2PBlockProvider? _p2PBlockProvider;
	public int MaximumParallelTasks { get; }

	/// <summary>Signals that there is a block-download request or multiple block-download requests.</summary>
	private readonly SemaphoreSlim _requestAvailableSemaphore = new(initialCount: 0, maxCount: 1);

	/// <summary>Block hashes that are to be downloaded. Block height represents priority of the priority queue.</summary>
	/// <remarks>
	/// Guarded by <see cref="_lock"/>.
	/// <para>Internal for testing purposes.</para>
	/// </remarks>
	internal PriorityQueue<Request, Priority> BlocksToDownloadRequests { get; } = new(Priority.Comparer);

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
	public async Task<IResult> TryGetBlockAsync(ISourceRequest sourceRequest, uint256 blockHash, Priority priority, CancellationToken cancellationToken)
	{
		Request request = new(sourceRequest, blockHash, priority, new TaskCompletionSource<IResult>());
		Enqueue(request);

		try
		{
			await request.Tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			request.Tcs.TrySetResult(CanceledResult.Instance);
		}

		// Now the task is guaranteed to return a result.
		return await request.Tcs.Task.ConfigureAwait(false);
	}

	private void Enqueue(Request request)
	{
		lock (_lock)
		{
			int count = BlocksToDownloadRequests.Count;
			BlocksToDownloadRequests.Enqueue(request, request.Priority);

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
		PriorityQueue<Request, Priority> tempQueue = new(Priority.Comparer);

		List<uint256> toRemoveFromCache = [];

		lock (_lock)
		{
			List<(Request Element, Priority Priority)> items = BlocksToDownloadRequests.UnorderedItems.ToList();

			foreach ((Request request, Priority priority) in items)
			{
				if (priority.BlockHeight <= maxBlockHeight)
				{
					tempQueue.Enqueue(request, priority);
				}
				else
				{
					// The block might have been downloaded by now so just try to set the result.
					request.Tcs.TrySetResult(new ReorgOccurredResult());
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
					request.Tcs.TrySetResult(CanceledResult.Instance);
				}
			}
		}
	}

	private async Task HandleSingleBlockTaskAsync(Request request, CancellationToken cancellationToken)
	{
		RequestResponse response = await DownloadSingleBlockAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Result is SuccessResult)
		{
			request.Tcs.TrySetResult(response.Result);
		}
		else
		{
			Logger.LogDebug($"Attempt to download block {request.BlockHash} (height: {request.Priority.BlockHeight}) failed.");

			// The block might have been removed concurrently if a reorg occurred.
			if (!request.Tcs.TrySetResult(response.Result))
			{
				IResult opResult = await request.Tcs.Task.ConfigureAwait(false);
				Logger.LogDebug($"Failed to set result for block '{request.BlockHash}' (height: {request.Priority.BlockHeight}). Result is already: {opResult}");
			}
		}
	}

	/// <summary>
	/// Downloads a single block from a block source, or gets the block from the file-system cache if available.
	/// </summary>
	private async Task<RequestResponse> DownloadSingleBlockAsync(Request request, CancellationToken cancellationToken)
	{
		Logger.LogTrace($"Trying to download {request.BlockHash} (height: {request.Priority.BlockHeight}).");

		try
		{
			// Try to get the block from the file-system storage.
			Block? block = await _fileSystemBlockRepository.TryGetAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);
			if (block is not null)
			{
				return new RequestResponse(new SuccessResult(block));
			}

			SuccessResult? successResult = null;
			bool failed = false;

			if (request.SourceRequest is TrustedFullNodeSourceRequest)
			{
				// Try to get the block from a trusted node, whether it's integrated or distant.
				if (_trustedFullNodeBlockProviders.Length == 0)
				{
					return new RequestResponse(NoSuchProviderResult.Instance);
				}

				foreach (IBlockProvider blockProvider in _trustedFullNodeBlockProviders)
				{
					block = await blockProvider.TryGetBlockAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);

					if (block is not null)
					{
						successResult = new(block);
						break;
					}
				}

				if (successResult is null)
				{
					failed = true;
				}
			}
			else if (request.SourceRequest is P2pSourceRequest p2pSourceRequest)
			{
				// Try to get the block from the P2P Network.
				if (_p2PBlockProvider is null)
				{
					return new RequestResponse(NoSuchProviderResult.Instance);
				}

				P2pBlockResponse response = await _p2PBlockProvider.TryGetBlockWithSourceDataAsync(request.BlockHash, p2pSourceRequest, cancellationToken).ConfigureAwait(false);

				if (response.Block is not null)
				{
					block = response.Block;
					successResult = new(block);
				}
				else
				{
					failed = true;
				}
			}

			// Store the block to the file-system.
			if (block is not null)
			{
				try
				{
					await _fileSystemBlockRepository.SaveAsync(block, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.LogError($"Failed to cache block {request.BlockHash} (height: {request.Priority.BlockHeight})", ex);
				}
			}

			if (successResult is not null)
			{
				return new RequestResponse(successResult);
			}

			if (failed)
			{
				return new RequestResponse(new FailureResult());
			}

			throw new UnreachableException();
		}
		catch (OperationCanceledException)
		{
			return new RequestResponse(CanceledResult.Instance);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Exception thrown while getting block {request.BlockHash} (height: {request.Priority.BlockHeight})", ex);
			throw new UnreachableException("Unexpected exception occurred", ex);
		}
	}

	/// <param name="Tcs">By design, this task completion source is not supposed to be ended by </param>
	internal record Request(ISourceRequest SourceRequest, uint256 BlockHash, Priority Priority, TaskCompletionSource<IResult> Tcs);
	private record RequestResponse(IResult Result);

	/// <summary>Block was downloaded successfully.</summary>
	/// <param name="SourceData">Source data for the bitcoin block.</param>
	public record SuccessResult(Block Block) : IResult;

	/// <summary>Block could not be get because a blockchain reorg occurred.</summary>
	/// <remarks>
	/// The result announces that getting a certain block does not really makes sense because it got evicted by other nodes.
	/// <para>There is a chance that one downloads given block sooner than a reorg is announced so that possibility must be taken into account by the caller.</para>
	/// </remarks>
	public record ReorgOccurredResult : IFailureResult;

	/// <summary>Block could not be get for an unknown reason from any block providing source.</summary>
	/// <remarks>Trying to get the block later might help or it might not. There is no guarantee.</remarks>
	public record FailureResult : IFailureResult;

	/// <summary>Block could not be get because specified block providing source was not registered.</summary>
	/// <remarks>Re-trying won't help. If no trusted full node is set, then this won't change.</remarks>
	public record NoSuchProviderResult : IFailureResult
	{
		public static readonly NoSuchProviderResult Instance = new();
	}

	/// <summary>Block could not be get because the service is shutting down.</summary>
	public record CanceledResult : IFailureResult
	{
		public static readonly CanceledResult Instance = new();
	}
}
