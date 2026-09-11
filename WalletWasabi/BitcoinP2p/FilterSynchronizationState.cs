using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Threading.Channels;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public class FilterSynchronizationState
{
	private static readonly TimeSpan HeaderAssignmentTimeout = TimeSpan.FromSeconds(25);
	private static readonly TimeSpan FilterAssignmentTimeout = TimeSpan.FromSeconds(30);
	private const int HeadersPerRequest = 2_000;
	private const int FiltersPerRequest = 500;
	private const int MaxLookaheadRanges = 25;

	private readonly Lock _lock = new();
	private readonly ConcurrentChain _blockHeaderChain;
	private readonly FilterHeaderChain _filterHeaderChain;
	private readonly EventBus? _eventBus;
	private readonly TimeProvider _timeProvider;

	private readonly RequestTracker<HeaderResponse> _headerTracker;
	private readonly RequestTracker<FilterResponse> _filterTracker;

	private readonly Channel<FilterResponse> _readyFiltersChannel;

	private readonly SortedDictionary<uint, UnvalidatedHeaderResponse> _bufferedHeaderResponses = [];

	public FilterSynchronizationState(ConcurrentChain blockHeaderChain, FilterHeaderChain filterHeaderChain, ChainHeight tipHeight,
		EventBus? eventBus = null, TimeProvider? timeProvider = null)
	{
		_blockHeaderChain = blockHeaderChain;
		_filterHeaderChain = filterHeaderChain;
		_eventBus = eventBus;
		_timeProvider = timeProvider ?? TimeProvider.System;

		var initialHeaderHeight = _filterHeaderChain.Tip?.Height ?? 0;
		_headerTracker = new RequestTracker<HeaderResponse>(_timeProvider, initialHeaderHeight);
		_readyFiltersChannel = Channel.CreateUnbounded<FilterResponse>();
		_filterTracker = new RequestTracker<FilterResponse>(_timeProvider, tipHeight);
	}

	internal bool TryAssignHeaderRange(Network network, [NotNullWhen(true)] out RangeRequest? assignment)
	{
		assignment = null;
		lock (_lock)
		{
			RewindTrackersToFilterHeaderTipNoLock();

			// Check for and release any stale header assignments
			if (_headerTracker.GetOldestStaleAssignment(HeaderAssignmentTimeout) is { } staleHeaderHeight)
			{
				_headerTracker.RemoveActiveAssignment(staleHeaderHeight);
				Logger.LogWarning(
					$"Auto-released stale filter header assignment at height {staleHeaderHeight}");
			}

			var chainTip = _blockHeaderChain.Tip;

			// Find the next range to assign (respecting max lookahead limit)
			// Also skip ranges that are already buffered awaiting validation
			if (!_headerTracker.TryGetNextRangeStartHeight(MaxLookaheadRanges, _bufferedHeaderResponses, out var nextRangeStart))
			{
				Logger.LogTrace(
					$"Max lookahead limit reached for headers (active: {_headerTracker.ActiveCount}, pending: {_headerTracker.PendingCount})");
				return false;
			}

			// Nothing to fetch if we're caught up
			if (nextRangeStart > chainTip.Height)
			{
				return false;
			}

			// Calculate stop height (limited by chain tip and the max number of headers to request)
			var stopHeight = (uint) Math.Min(nextRangeStart + HeadersPerRequest - 1, chainTip.Height);

			// Get the stop block hash
			var stopBlock = _blockHeaderChain.GetBlock((int) stopHeight);

			// Track this assignment so other nodes don't get the same range
			_headerTracker.AddActiveAssignment(nextRangeStart, stopHeight);

			assignment = new RangeRequest(nextRangeStart, stopHeight, stopBlock.HashBlock);
			return true;
		}
	}

	private void OnHeaderCompletedNoLock(uint rangeStartHeight, SmartHeader[] headers)
	{
		var response = new HeaderResponse(rangeStartHeight, headers);
		if (!_headerTracker.TryMoveActiveToPending(rangeStartHeight, response))
		{
			Logger.LogDebug($"Ignoring stale filter header range {rangeStartHeight}-{response.EndHeight} (already processed up to {_headerTracker.LastHeight})");
			return;
		}

		// Process any ranges that are now ready
		ProcessPendingHeaderRangesNoLock();

		// Try to validate and process any buffered responses that may now be ready
		TryProcessBufferedHeadersNoLock();
	}

	internal void OnHeaderNodeDisconnected(RangeRequest assignment)
	{
		lock (_lock)
		{
			_headerTracker.RemoveActiveAssignment(assignment.StartHeight);
		}

		Logger.LogDebug(
			$"Node disconnected, released filter header range {assignment.StartHeight} for reassignment");
	}

	internal HeaderValidationResult ValidateFilterHeaders(
		RangeRequest assignment,
		uint256[] filterHashes,
		uint256 declaredPreviousFilterHeader,
		Network network)
	{
		lock (_lock)
		{
			// Recalculate the expected previous filter header from the chain
			if (!TryGetPreviousFilterHeader(assignment.StartHeight, network, out var expectedPreviousFilterHeader))
			{
				Logger.LogDebug($"Previous filter header not yet available for range {assignment.StartHeight}, will retry later");
				BufferUnvalidatedHeadersNoLock(assignment, filterHashes, declaredPreviousFilterHeader, network);
				return new HeaderValidationResult.NotReadyYet();
			}

			// Validate against the expected previous filter header
			if (declaredPreviousFilterHeader != expectedPreviousFilterHeader)
			{
				var reason = $"Previous filter header mismatch for range {assignment.StartHeight} - expected {expectedPreviousFilterHeader}, received {declaredPreviousFilterHeader}";
				Logger.LogWarning(reason);
				return new HeaderValidationResult.Invalid(reason);
			}

			var result = new SmartHeader[filterHashes.Length];
			var prevFilterHeader = declaredPreviousFilterHeader;

			for (var i = 0; i < filterHashes.Length; i++)
			{
				var height = assignment.StartHeight + (uint)i;
				var block = _blockHeaderChain.GetBlock((int)height);
				if (block == null)
				{
					var reason = $"Block header not found at height {height}, aborting batch validation";
					Logger.LogWarning(reason);

					return new HeaderValidationResult.Invalid(reason);
				}

				var filterHash = filterHashes[i];
				var filterHeader = ComputeFilterHeader(filterHash, prevFilterHeader);

				result[i] = new SmartHeader(
					block.HashBlock,
					filterHeader,
					height,
					block.Header.BlockTime);

				prevFilterHeader = filterHeader;
			}

			OnHeaderCompletedNoLock(assignment.StartHeight, result);

			return new HeaderValidationResult.Success(result);
		}
	}

	/// <summary>
	/// Buffers a header response that arrived before we could validate it.
	/// Removes the assignment from active tracking since we have the data.
	/// </summary>
	private void BufferUnvalidatedHeadersNoLock(RangeRequest assignment, uint256[] filterHashes, uint256 declaredPreviousFilterHeader, Network network)
	{
		// Remove from active assignments - we have the data, just can't validate yet
		_headerTracker.RemoveActiveAssignment(assignment.StartHeight);

		_bufferedHeaderResponses[assignment.StartHeight] = new UnvalidatedHeaderResponse(
			assignment, filterHashes, declaredPreviousFilterHeader, network);

		Logger.LogDebug(
			$"Buffered filter header range {assignment.StartHeight}-{assignment.StopHeight} for later validation. " +
			$"Total buffered: {_bufferedHeaderResponses.Count}");
	}

	/// <summary>
	/// Attempts to validate and process any buffered header responses that are now ready.
	/// Called after a range completes, since the previous filter header may now be available.
	/// </summary>
	private void TryProcessBufferedHeadersNoLock()
	{
		// Process buffered responses in order
		while (true)
		{
			var nextExpected = _headerTracker.LastHeight + 1;

			if (!_bufferedHeaderResponses.TryGetValue(nextExpected, out var buffered))
			{
				break;
			}

			// Try to validate now
			var result = ValidateFilterHeaders(
				buffered.Assignment,
				buffered.FilterHashes,
				buffered.DeclaredPreviousFilterHeader,
				buffered.Network);

			if (result is not HeaderValidationResult.Success success)
			{
				// Still not ready or invalid - leave it buffered (NotReadyYet shouldn't happen here)
				// or remove it if invalid
				if (result is HeaderValidationResult.Invalid)
				{
					_bufferedHeaderResponses.Remove(nextExpected);
					Logger.LogWarning($"Buffered header range {nextExpected} failed validation, discarding");
				}
				break;
			}

			// Validation succeeded - remove from buffer
			_bufferedHeaderResponses.Remove(nextExpected);
			Logger.LogInfo($"Successfully validated buffered filter header range {buffered.Assignment}");

			// Process the headers directly (buffered responses are not in active assignments)
			foreach (var header in success.Headers)
			{
				try
				{
					_filterHeaderChain.AppendTip(header);
					_headerTracker.SetLastHeight(header.Height);
				}
				catch (InvalidOperationException ex)
				{
					Logger.LogError($"Failed to append buffered filter header at height {header.Height}: {ex.Message}");
					return;
				}
			}

			_eventBus?.Publish(new FilterHeadersTipChanged(_headerTracker.LastHeight));
			Logger.LogInfo($"Successfully processed buffered filter header range {buffered.Assignment.StartHeight}, new tip at height {_headerTracker.LastHeight}");
		}
	}

	private void ProcessPendingHeaderRangesNoLock()
	{
		// Process ranges in order
		while (true)
		{
			var nextExpectedStart = _headerTracker.LastHeight + 1;

			if (!_headerTracker.TryRemovePendingRange(nextExpectedStart, out var pendingRange))
			{
				// Next range not available yet
				break;
			}

			// Append all headers from this range
			foreach (var header in pendingRange.Headers)
			{
				try
				{
					_filterHeaderChain.AppendTip(header);
					_headerTracker.SetLastHeight(header.Height);
				}
				catch (InvalidOperationException ex)
				{
					Logger.LogError($"Failed to append filter header at height {header.Height}: {ex.Message}");
					// Stop processing - this shouldn't happen since headers were pre-validated
					return;
				}
			}

			_eventBus?.Publish(new FilterHeadersTipChanged(_headerTracker.LastHeight));

			Logger.LogInfo(
				$"Successfully processed filter header range {pendingRange.StartHeight}, new tip at height {_headerTracker.LastHeight}");
		}
	}

	private static uint256 ComputeFilterHeader(uint256 filterHash, uint256 prevFilterHeader)
	{
		Span<byte> data = stackalloc byte[64];
		filterHash.ToBytes(data[..32]);
		prevFilterHeader.ToBytes(data[32..]);
		Span<byte> hash = stackalloc byte[32];
		SHA256.HashData(SHA256.HashData(data), hash);
		return new uint256(hash);
	}

	public bool TryGetPreviousFilterHeader(uint startHeight, Network network, [NotNullWhen(true)] out uint256? header)
	{
		header = startHeight == 1
			? network == Network.Main ? uint256.Zero : FilterCheckpoints.GetWasabiGenesisFilter(network).Header.BlockFilterHeader
			: _filterHeaderChain[startHeight - 1]?.BlockFilterHeader;

		return header is not null;
	}

	public async Task<FilterModel[]> GetNextFilterBatchAsync(CancellationToken cancellationToken)
	{
		var filterResponse = await _readyFiltersChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogDebug($"Filter range {filterResponse.StartHeight} consumed ({filterResponse.Filters.Length} filters)");
		return filterResponse.Filters;
	}

	internal bool TryAssignFilterRange([NotNullWhen(true)] out RangeRequest? assignment)
	{
		assignment = null;
		lock (_lock)
		{
			RewindTrackersToFilterHeaderTipNoLock();

			// Check for and release any stale filter assignments
			if (_filterTracker.GetOldestStaleAssignment(FilterAssignmentTimeout) is { } staleFilterHeight)
			{
				_filterTracker.RemoveActiveAssignment(staleFilterHeight);
				Logger.LogWarning($"Auto-released stale filter assignment at height {staleFilterHeight}");
			}

			// Check if filter headers are synced ahead
			var filterHeadersTip = _filterHeaderChain.Tip;
			if (filterHeadersTip is null)
			{
				// Filter headers not yet synced - can't assign filter ranges yet
				return false;
			}

			// Find the next range to assign (respecting max lookahead limit)
			if (!_filterTracker.TryGetNextRangeStartHeight(MaxLookaheadRanges, out var nextRangeStart))
			{
				Logger.LogTrace(
					$"Max lookahead limit reached (active: {_filterTracker.ActiveCount}, pending: {_filterTracker.PendingCount})");
				return false;
			}

			// Nothing to fetch if we're caught up with filter headers
			if (nextRangeStart > filterHeadersTip.Height)
			{
				Logger.LogTrace(
					$"caught up (next range start {nextRangeStart} > filter headers tip {filterHeadersTip.Height})");
				return false;
			}

			// Calculate stop height (limited by filter headers tip and page size)
			var stopHeight = Math.Min(nextRangeStart + FiltersPerRequest - 1, filterHeadersTip.Height);

			// Get the stop block hash
			var stopBlock = _blockHeaderChain.GetBlock((int) stopHeight);

			// Track this assignment so other nodes don't get the same page
			_filterTracker.AddActiveAssignment(nextRangeStart, stopHeight);

			Logger.LogDebug(
				$"Assigned filter page {nextRangeStart}-{stopHeight} (active: {_filterTracker.ActiveCount}, pending: {_filterTracker.PendingCount})");

			assignment = new RangeRequest(nextRangeStart, stopHeight, stopBlock.HashBlock);
			return true;
		}
	}

	public void OnFilterRangeCompleted(uint rangeStartHeight, FilterModel[] filters)
	{
		lock (_lock)
		{
			var response = new FilterResponse(rangeStartHeight, filters);
			if (!_filterTracker.TryMoveActiveToPending(rangeStartHeight, response))
			{
				Logger.LogDebug(
					$"Ignoring stale filter range {rangeStartHeight}-{response.EndHeight} (already processed up to {_filterTracker.LastHeight})");
				return;
			}

			Logger.LogTrace(
				$"State after filter range completion - active: {_filterTracker.ActiveCount}, pending: {_filterTracker.PendingCount}, next expected: {_filterTracker.LastHeight + 1}");

			// Try to make the next range ready for consumption
			TryMakeNextFilterRangeReadyNoLock();
		}
	}

	internal void OnFilterNodeDisconnected(RangeRequest assignment)
	{
		lock (_lock)
		{
			_filterTracker.RemoveActiveAssignment(assignment.StartHeight);
		}

		Logger.LogDebug($"Node disconnected, released filter range {assignment.StartHeight} for reassignment");
	}

	public uint256? GetExpectedFilterHeader(uint height)
	{
		return _filterHeaderChain[height]?.BlockFilterHeader;
	}

	public bool IsReorg(uint fromHeight, uint256 fromHash)
	{
		lock (_lock)
		{
			var filterTip = _filterHeaderChain.Tip;
			if (filterTip is null)
			{
				return false;
			}

			var anchorBlock = _blockHeaderChain.GetBlock((int)fromHeight);
			if (anchorBlock is null || anchorBlock.HashBlock != fromHash)
			{
				return MarkReorg($"Block {fromHash} at height {fromHeight} not found or mismatched in block header chain. Reorg detected.");
			}

			var start = filterTip.Height;
			var endInclusive = start -  ChainHeight.Min((uint)(_filterHeaderChain.Count - 1), 100u);

			// Compare filter headers against block headers from tip backwards.
			for (long height = start; height >= endInclusive; height--)
			{
				var h = (uint)height;

				var filterHeader = _filterHeaderChain[h];
				if (filterHeader is null)
				{
					return MarkReorg($"Missing filter header at height {h}. Reorg detected.");
				}

				var block = _blockHeaderChain.GetBlock((int)h);
				if (block is null)
				{
					return MarkReorg($"Missing block header at height {h}. Reorg detected.");
				}

				if (filterHeader.BlockHash != block.HashBlock)
				{
					return MarkReorg($"Hash mismatch at height {h}. Filter={filterHeader.BlockHash}, Block={block.HashBlock}. Reorg detected.");
				}
			}

			return false;

			bool MarkReorg(string message)
			{
				Logger.LogInfo(message);
				ClearPendingStateNoLock();
				return true;
			}
		}
	}

	/// <summary>
	/// After a reorg rollback the filter header chain tip moves below the trackers'
	/// <see cref="RequestTracker{TProcessedResponse}.LastHeight"/>; pull them back so the next request
	/// re-fetches the replaced height instead of skipping past it.
	/// </summary>
	private void RewindTrackersToFilterHeaderTipNoLock()
	{
		if (_filterHeaderChain.Tip is not { } tip)
		{
			return;
		}

		var tipHeight = (uint)tip.Height;
		if (_headerTracker.LastHeight > tipHeight)
		{
			_headerTracker.RewindTo(tipHeight);
		}

		if (_filterTracker.LastHeight > tipHeight)
		{
			_filterTracker.RewindTo(tipHeight);
		}
	}

	private void ClearPendingStateNoLock()
	{
		_headerTracker.ClearAllPending();
		_filterTracker.ClearAllPending();
		_bufferedHeaderResponses.Clear();

		Logger.LogDebug("Cleared all pending filter header and filter assignments due to reorg");
	}

	private void TryMakeNextFilterRangeReadyNoLock()
	{
		var lastQueuedHeight = _filterTracker.LastHeight;

		while (true)
		{
			var nextExpectedStart = lastQueuedHeight + 1;

			if (!_filterTracker.TryRemovePendingRange(nextExpectedStart, out var pendingRange))
			{
				// Next page not available yet
				if (_filterTracker.HasPendingRanges)
				{
					var pendingHeights = string.Join(", ", _filterTracker.GetPendingHeights());
					Logger.LogTrace(
						$"Next expected filter range {nextExpectedStart} not available yet. Pending ranges at heights: {pendingHeights}");
				}
				else
				{
					Logger.LogTrace(
						$"Next expected filter range {nextExpectedStart} not available yet. No pending ranges.");
				}

				return;
			}

			// Write to channel (always succeeds with unbounded channel)
			_readyFiltersChannel.Writer.TryWrite(pendingRange);

			lastQueuedHeight = pendingRange.EndHeight;
			_filterTracker.SetLastHeight(lastQueuedHeight);

			Logger.LogDebug(
				$"Filter range {pendingRange.StartHeight} queued for consumption ({pendingRange.Filters.Length} filters)");
		}
	}

	/// <summary>
	/// Result of filter header validation attempt.
	/// </summary>
	public abstract record HeaderValidationResult
	{
		/// <summary>Validation succeeded.</summary>
		public record Success(SmartHeader[] Headers) : HeaderValidationResult;

		/// <summary>Previous filter header not available yet - buffer and retry later.</summary>
		public record NotReadyYet : HeaderValidationResult;

		/// <summary>Validation failed - peer sent invalid data.</summary>
		public record Invalid(string Reason) : HeaderValidationResult;
	}
}
