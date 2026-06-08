using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public class CompactFilterBehavior(
	CompactFilterBehavior.FilterSynchronizationState synchronizationState,
	ConcurrentChain blockHeaderChain,
	EventBus eventBus)
	: NodeBehavior
{
	private static readonly TimeSpan TickSyncInterval = TimeSpan.FromSeconds(2);

	private readonly List<CompactFilterPayload> _collectedFilters = [];

	private RangeRequest? _assignedHeaderRange;
	private RangeRequest? _assignedFilterRange;

	private volatile bool _invalidReceived;

	public CompactFilterBehavior(ConcurrentChain blockHeaders, FilterHeaderChain filterHeaders, ChainHeight tipHeight,
		EventBus eventBus)
		: this(new FilterSynchronizationState(blockHeaders, filterHeaders, tipHeight), blockHeaders, eventBus)
	{
	}

	public CompactFilterBehavior(ConcurrentChain blockHeaders, FilterHeaderChain filterHeaders, EventBus eventBus)
		: this(new FilterSynchronizationState(blockHeaders, filterHeaders), blockHeaders, eventBus)
	{
	}

	protected override void AttachCore()
	{
		AttachedNode.StateChanged += OnStateChanged;
		AttachedNode.MessageReceived += OnMessageReceived;
	}

	protected override void DetachCore()
	{
		AttachedNode.StateChanged -= OnStateChanged;
		AttachedNode.MessageReceived -= OnMessageReceived;

		ReleaseAssignments();
	}

	public override object Clone() =>
		new CompactFilterBehavior(synchronizationState, blockHeaderChain, eventBus);

	private void OnStateChanged(Node node, NodeState oldState)
	{
		Logger.LogDebug($"Node {node.Peer.Endpoint} state changed from {oldState} to {node.State}");

		// Once the handshake completes, check if node supports compact filters
		if (node.State != NodeState.HandShaked)
		{
			return;
		}

		if (!node.SupportsCompactFilters)
		{
			Logger.LogDebug($"Node {node.Peer.Endpoint} does not support NODE_COMPACT_FILTERS, will not sync");
			return;
		}

		Logger.LogDebug($"Node {node.Peer.Endpoint} supports NODE_COMPACT_FILTERS, starting sync");

		// Subscribe to tick events for periodic sync attempts
		var lastTickSync = DateTime.MinValue;
		var tickSubscription = eventBus.Subscribe<Tick>(tick =>
		{
			var nowUtc = tick.DateTime;
			if (nowUtc - lastTickSync < TickSyncInterval)
			{
				return;
			}

			lastTickSync = tick.DateTime;
			TrySync(node);
		});
		RegisterDisposable(tickSubscription);

		TrySync(node);
	}

	private void OnMessageReceived(Node node, IncomingMessage message)
	{
		if (!IsNodeInValidState(node))
		{
			return;
		}

		if (_assignedHeaderRange is { } assignedHeaderRange &&
		    message.Message.Payload is CompactFilterHeadersPayload {FilterType: FilterType.Basic} cfHeaders)
		{
			HandleFilterHeaderMessage(node, cfHeaders, assignedHeaderRange);
			return;
		}

		if (_assignedFilterRange is { } assignedFilterRange &&
		    message.Message.Payload is CompactFilterPayload {FilterType: FilterType.Basic} filterPayload)
		{
			HandleFilterMessage(node, filterPayload, assignedFilterRange);
		}
	}

	private void HandleFilterHeaderMessage(Node node, CompactFilterHeadersPayload cfHeaders, RangeRequest assignment)
	{
		var filterHashes = cfHeaders.FilterHeaders;
		var batchCount = filterHashes.Count;

		Logger.LogDebug(
			$"Received {batchCount} filter headers from {node.Peer.Endpoint} for range {assignment}");

		if (batchCount == 0)
		{
			Logger.LogDebug("Received empty cfheaders batch");
			HandleInvalid(node, "Invalid compact filter headers received");
			return;
		}

		// Resolve the stop block to validate the response
		var stopBlock = blockHeaderChain.GetBlock(cfHeaders.StopHash);
		if (stopBlock == null)
		{
			return;
		}

		var startHeight = stopBlock.Height - batchCount + 1;
		if (startHeight < 0)
		{
			Logger.LogWarning(
				$"Invalid batch - start height {startHeight} is negative (stopHeight={stopBlock.Height}, batchCount={batchCount})");
			HandleInvalid(node, "Invalid compact filter headers received");
			return;
		}

		// Verify this matches our assignment
		if ((uint) startHeight != assignment.StartHeight)
		{
			Logger.LogWarning(
				$"Received headers for wrong range - expected {assignment.StartHeight}, got {startHeight}");
			HandleInvalid(node, "Invalid compact filter headers received");
			return;
		}

		// Validate headers using shared state
		var validatedHeaders = synchronizationState.ValidateFilterHeaders(
			assignment,
			filterHashes.ToArray(),
			cfHeaders.PreviousFilterHeader);

		if (validatedHeaders is null)
		{
			Logger.LogWarning(
				$"Validation failed for filter header range {assignment}");
			HandleInvalid(node, "Invalid compact filter headers received");
			return;
		}

		Logger.LogInfo($"Successfully validated filter header range {assignment}");

		// Report success to shared state
		synchronizationState.OnHeaderCompleted(assignment.StartHeight, validatedHeaders);
		_assignedHeaderRange = null;

		// Immediately try to fetch the next range
		TrySync(node);
	}

	private void HandleFilterMessage(Node node, CompactFilterPayload filterPayload, RangeRequest assignment)
	{
		_collectedFilters.Add(filterPayload);

		// Check if we've received all filters for this range
		if (filterPayload.BlockHash != assignment.StopHash && _collectedFilters.Count < assignment.Count)
		{
			return;
		}

		Logger.LogDebug(
			$"Range {assignment} complete ({_collectedFilters.Count}/{assignment.Count} filters)");

		var filters = _collectedFilters.ToArray();
		_collectedFilters.Clear();

		// Validate all filters
		var validatedFilters = ValidateFilters(assignment.StartHeight, filters);

		if (validatedFilters is null)
		{
			// Validation failed - disconnect and release assignment
			Logger.LogWarning($"Validation failed for range {assignment}");
			HandleInvalid(node, "Invalid compact filters received");
			return;
		}

		Logger.LogInfo($"Successfully validated range {assignment}");

		// Report success
		synchronizationState.OnFilterRangeCompleted(assignment.StartHeight, validatedFilters);
		_assignedFilterRange = null;

		// Immediately try to fetch the next range
		TrySync(node);
	}

	private FilterModel[]? ValidateFilters(uint startHeight, CompactFilterPayload[] filters)
	{
		if (filters.Length == 0)
		{
			Logger.LogWarning("Received empty filter batch");
			return null;
		}

		var result = new FilterModel[filters.Length];

		// For the first filter, we need the previous filter header
		// This comes from either the shared state (last emitted) or the filter header chain
		var prevFilterHeader = startHeight == 1
			? uint256.Zero
			: synchronizationState.GetExpectedFilterHeader(startHeight - 1);

		if (prevFilterHeader is null)
		{
			Logger.LogWarning(
				$"Cannot validate filters: previous filter header not available for height {startHeight}");
			return null;
		}

		for (var i = 0; i < filters.Length; i++)
		{
			var filterPayload = filters[i];
			var height = startHeight + (uint) i;

			// Get expected filter header from the pre-synced chain
			var expectedHeader = synchronizationState.GetExpectedFilterHeader(height);
			if (expectedHeader is null)
			{
				Logger.LogWarning($"Cannot validate filter: expected header not available for height {height}");
				return null;
			}

			// Compute actual filter header
			var grFilterResult = Result<GolombRiceFilter, Exception>
				.Catch(() => new GolombRiceFilter(filterPayload.FilterBytes));

			if (!grFilterResult.IsOk)
			{
				Logger.LogWarning($"Malformed Golomb-Rice filter at height {height}: {grFilterResult.Error.Message}");
				return null;
			}

			var grFilter = grFilterResult.Value;
			var actualHeader = grFilter.GetHeader(prevFilterHeader);

			// Validate
			if (actualHeader != expectedHeader)
			{
				Logger.LogWarning(
					$"Invalid filter at height {height}: expected header {expectedHeader}, got {actualHeader}");
				return null;
			}

			// Get block info for the SmartHeader
			var block = blockHeaderChain.GetBlock((int) height);
			if (block is null)
			{
				Logger.LogWarning($"Block header not available for height {height}");
				return null;
			}

			var smartHeader = new SmartHeader(
				filterPayload.BlockHash,
				actualHeader,
				height,
				block.Header.BlockTime.ToUnixTimeSeconds());

			result[i] = new FilterModel(smartHeader, grFilter);

			// Update for next iteration
			prevFilterHeader = actualHeader;
		}

		return result;
	}

	private void TrySync(Node node)
	{
		if (!IsNodeInValidState(node))
		{
			return;
		}

		TrySyncHeaders(node);
		TrySyncFilters(node);
	}

	private void TrySyncFilters(Node node)
	{
		if (_assignedFilterRange is not null)
		{
			return;
		}

		if (!synchronizationState.TryAssignFilterRange(out var filterAssignment))
		{
			return;
		}

		_assignedFilterRange = filterAssignment;
		Logger.LogDebug(
			$"Assigned range {filterAssignment} to node {node.Peer.Endpoint}");

		_collectedFilters.Clear();

		var payload = new GetCompactFiltersPayload(FilterType.Basic, filterAssignment.StartHeight,
			filterAssignment.StopHash);
		node.SendMessage(payload);
	}

	private void TrySyncHeaders(Node node)
	{
		if (_assignedHeaderRange is not null)
		{
			return;
		}

		if (!synchronizationState.TryAssignHeaderRange(out var headerAssignment))
		{
			return;
		}

		_assignedHeaderRange = headerAssignment;
		Logger.LogDebug($"Assigned filter header range {headerAssignment} to node {node.Peer.Endpoint}");

		var payload = new GetCompactFilterHeadersPayload(FilterType.Basic, headerAssignment.StartHeight,
			headerAssignment.StopHash);
		node.SendMessage(payload);
	}

	private void HandleInvalid(Node node, string reason)
	{
		_invalidReceived = true;

		ReleaseAssignments();

		Logger.LogWarning($"Disconnecting node {node.Peer.Endpoint}: {reason}");

		// Disconnect the node
		node.DisconnectAsync(reason);
	}

	private void ReleaseAssignments()
	{
		if (_assignedHeaderRange is not null)
		{
			synchronizationState.OnHeaderNodeDisconnected(_assignedHeaderRange);
			_assignedHeaderRange = null;
		}

		if (_assignedFilterRange is not null)
		{
			synchronizationState.OnFilterNodeDisconnected(_assignedFilterRange);
			_assignedFilterRange = null;
		}

		_collectedFilters.Clear();
	}

	private bool IsNodeInValidState(Node node)
	{
		if (_invalidReceived)
		{
			return false;
		}

		return node is {State: NodeState.HandShaked};
	}

	public class FilterSynchronizationState
	{
		private static readonly TimeSpan HeaderAssignmentTimeout = TimeSpan.FromSeconds(25);
		private static readonly TimeSpan FilterAssignmentTimeout = TimeSpan.FromSeconds(30);
		private const int HeadersPerRequest = 1_100;
		private const int FiltersPerRequest = 400;
		private const int MaxLookaheadRanges = 25;

		private readonly Lock _lock = new();
		private readonly ConcurrentChain _blockHeaderChain;
		private readonly FilterHeaderChain _filterHeaderChain;
		private readonly TimeProvider _timeProvider;

		private readonly RequestTracker<HeaderResponse> _headerTracker;
		private readonly RequestTracker<FilterResponse> _filterTracker;

		private readonly Channel<FilterResponse> _readyFiltersChannel;

		public FilterSynchronizationState(ConcurrentChain blockHeaderChain, FilterHeaderChain filterHeaderChain, TimeProvider? timeProvider = null)
		{
			_blockHeaderChain = blockHeaderChain;
			_filterHeaderChain = filterHeaderChain;
			_timeProvider = timeProvider ?? TimeProvider.System;

			var initialHeaderHeight = _filterHeaderChain.Tip?.Height ?? 0;
			_headerTracker = new RequestTracker<HeaderResponse>(_timeProvider, initialHeaderHeight);
			_filterTracker = new RequestTracker<FilterResponse>(_timeProvider, 0);

			_readyFiltersChannel = Channel.CreateUnbounded<FilterResponse>();
		}

		public FilterSynchronizationState(ConcurrentChain blockHeaders, FilterHeaderChain filterHeaders,
			ChainHeight tipHeight, TimeProvider? timeProvider = null)
			: this(blockHeaders, filterHeaders, timeProvider)
		{
			_filterTracker = new RequestTracker<FilterResponse>(_timeProvider, tipHeight);
		}

		internal bool TryAssignHeaderRange([NotNullWhen(true)] out RangeRequest? assignment)
		{
			assignment = null;
			lock (_lock)
			{
				// Check for and release any stale header assignments
				if (_headerTracker.GetOldestStaleAssignment(HeaderAssignmentTimeout) is { } staleHeaderHeight)
				{
					_headerTracker.RemoveActiveAssignment(staleHeaderHeight);
					Logger.LogWarning(
						$"Auto-released stale filter header assignment at height {staleHeaderHeight}");
				}

				var chainTip = _blockHeaderChain.Tip;

				// Find the next range to assign (respecting max lookahead limit)
				if (!_headerTracker.TryGetNextRangeStartHeight(MaxLookaheadRanges, out var nextRangeStart))
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

				// Don't assign this range if we don't have the previous filter header yet
				if (!TryGetPreviousFilterHeader(nextRangeStart, out _))
				{
					return false;
				}

				// Track this assignment so other nodes don't get the same range
				_headerTracker.AddActiveAssignment(nextRangeStart, stopHeight);

				assignment = new RangeRequest(nextRangeStart, stopHeight, stopBlock.HashBlock);
				return true;
			}
		}

		public void OnHeaderCompleted(uint rangeStartHeight, SmartHeader[] headers)
		{
			lock (_lock)
			{
				var response = new HeaderResponse(rangeStartHeight, headers);
				if (!_headerTracker.TryMoveActiveToPending(rangeStartHeight, response))
				{
					Logger.LogDebug(
						$"Ignoring stale filter header range {rangeStartHeight}-{response.EndHeight} (already processed up to {_headerTracker.LastHeight})");
					return;
				}

				// Process any ranges that are now ready
				ProcessPendingHeaderRanges();
			}
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

		internal SmartHeader[]? ValidateFilterHeaders(
			RangeRequest assignment,
			uint256[] filterHashes,
			uint256 declaredPreviousFilterHeader)
		{
			// Recalculate the expected previous filter header from the chain
			if (!TryGetPreviousFilterHeader(assignment.StartHeight, out var expectedPreviousFilterHeader))
			{
				Logger.LogWarning(
					$"Cannot validate: previous filter header not available for range {assignment.StartHeight}");
				return null;
			}

			// Validate against the expected previous filter header
			if (declaredPreviousFilterHeader != expectedPreviousFilterHeader)
			{
				Logger.LogWarning(
					$"Previous filter header mismatch for range {assignment.StartHeight} - expected {expectedPreviousFilterHeader}, received {declaredPreviousFilterHeader}");
				return null;
			}

			var result = new SmartHeader[filterHashes.Length];
			var prevFilterHeader = declaredPreviousFilterHeader;

			for (var i = 0; i < filterHashes.Length; i++)
			{
				var height = assignment.StartHeight + (uint) i;
				var block = _blockHeaderChain.GetBlock((int) height);
				if (block == null)
				{
					Logger.LogWarning($"Block header not found at height {height}, aborting batch validation");
					return null;
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

			return result;
		}

		private void ProcessPendingHeaderRanges()
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

		private bool TryGetPreviousFilterHeader(uint startHeight, [NotNullWhen(true)] out uint256? header)
		{
			header = startHeight == 1
				? uint256.Zero
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
				// Check for and release any stale filter assignments
				if (_filterTracker.GetOldestStaleAssignment(FilterAssignmentTimeout) is { } staleFilterHeight)
				{
					_filterTracker.RemoveActiveAssignment(staleFilterHeight);
					Logger.LogWarning($"Auto-released stale filter assignment at height {staleFilterHeight}");
				}

				// Check if filter headers are synced ahead
				var filterHeadersTip = _filterHeaderChain.Tip!;

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
				var endInclusive = Math.Max(0, start - 100 + 1);

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

		private void ClearPendingStateNoLock()
		{
			// Clear all pending header ranges
			_headerTracker.ClearAllPending();

			// Clear all pending filter ranges
			_filterTracker.ClearAllPending();

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
	}

	class RequestTracker<TProcessedResponse>(TimeProvider timeProvider, uint initialHeight = 0) where TProcessedResponse : Response
	{
		private readonly record struct ActiveAssignment(uint EndHeight, DateTime AssignedAt);

		private readonly TimeProvider _timeProvider = timeProvider;
		private readonly SortedDictionary<uint, TProcessedResponse> _pendingResponses = [];
		private readonly SortedDictionary<uint, ActiveAssignment> _activeAssignments = [];

		/// <summary>
		/// The last height that was processed/emitted.
		/// </summary>
		public uint LastHeight { get; private set; } = initialHeight;

		/// <summary>
		/// Number of pages currently being fetched.
		/// </summary>
		public int ActiveCount => _activeAssignments.Count;

		/// <summary>
		/// Number of completed pages awaiting processing.
		/// </summary>
		public int PendingCount => _pendingResponses.Count;

		/// <summary>
		/// Marks a range as actively being fetched.
		/// </summary>
		public void AddActiveAssignment(uint startHeight, uint endHeight)
		{
			_activeAssignments.Add(startHeight, new ActiveAssignment(endHeight, _timeProvider.GetUtcNow().UtcDateTime));
		}

		/// <summary>
		/// Removes an active assignment (on completion or node disconnect).
		/// </summary>
		public void RemoveActiveAssignment(uint startHeight)
		{
			_activeAssignments.Remove(startHeight);
		}

		/// <summary>
		/// Moves a completed range from active to pending.
		/// Returns false if the range is stale (already processed), true if added to pending.
		/// </summary>
		public bool TryMoveActiveToPending(uint startHeight, TProcessedResponse range)
		{
			RemoveActiveAssignment(startHeight);

			// Check if this range has already been processed (stale duplicate from slow node)
			if (range.EndHeight <= LastHeight)
			{
				return false;
			}

			_pendingResponses[startHeight] = range;
			return true;
		}

		/// <summary>
		/// Attempts to remove and return a pending range at the specified height.
		/// </summary>
		public bool TryRemovePendingRange(uint startHeight, [NotNullWhen(true)] out TProcessedResponse? range)
		{
			if (_pendingResponses.Remove(startHeight, out var removedResponse))
			{
				range = removedResponse;
				return true;
			}

			range = null;
			return false;
		}

		/// <summary>
		/// Gets all pending range heights (for logging).
		/// </summary>
		public IEnumerable<uint> GetPendingHeights()
		{
			return _pendingResponses.Keys.OrderBy(k => k);
		}

		/// <summary>
		/// Checks if there are any pending pages.
		/// </summary>
		public bool HasPendingRanges => _pendingResponses.Count > 0;

		/// <summary>
		/// Updates the last processed/emitted height.
		/// </summary>
		public void SetLastHeight(uint height)
		{
			LastHeight = height;
		}

		public bool TryGetNextRangeStartHeight(int maxLookaheadRanges, out uint startHeight)
		{
			var nextStart = LastHeight + 1;
			var rangeCount = 0;

			// Skip over any ranges that are already assigned or pending
			while (true)
			{
				if (_activeAssignments.TryGetValue(nextStart, out var active))
				{
					nextStart = active.EndHeight + 1;
					rangeCount++;
				}
				else if (_pendingResponses.TryGetValue(nextStart, out var pending))
				{
					nextStart = pending.EndHeight + 1;
					rangeCount++;
				}
				else
				{
					break;
				}

				if (rangeCount >= maxLookaheadRanges)
				{
					startHeight = 0u;
					return false;
				}
			}

			startHeight = nextStart;
			return true;
		}

		/// <summary>
		/// Finds the oldest active assignment that is older than the specified timeout.
		/// Returns null if no stale assignments found.
		/// </summary>
		public uint? GetOldestStaleAssignment(TimeSpan timeout)
		{
			var cutoffTime = _timeProvider.GetUtcNow().UtcDateTime - timeout;

			return _activeAssignments
				.Where(kvp => kvp.Value.AssignedAt < cutoffTime)
				.OrderBy(kvp => kvp.Value.AssignedAt)
				.Select(kvp => (uint?) kvp.Key)
				.FirstOrDefault();
		}

		/// <summary>
		/// Clears all pending assignments. Used when a reorg is detected.
		/// </summary>
		public void ClearAllPending()
		{
			_pendingResponses.Clear();
		}
	}

	internal record RangeRequest(uint StartHeight, uint StopHeight, uint256 StopHash)
	{
		public uint Count => StopHeight - StartHeight + 1;
		public override string ToString() => $"{StartHeight}-{StopHeight}";
	}

	private abstract record Response(uint StartHeight)
	{
		public abstract uint EndHeight { get; }
	}

	private record HeaderResponse(uint StartHeight, SmartHeader[] Headers) : Response(StartHeight)
	{
		public override uint EndHeight => Headers[^1].Height;
	}

	private record FilterResponse(uint StartHeight, FilterModel[] Filters) : Response(StartHeight)
	{
		public override uint EndHeight => Filters[^1].Header.Height;
	}
}
