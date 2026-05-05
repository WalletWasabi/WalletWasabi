using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public class CompactFilterBehavior(
	FilterSynchronizationState synchronizationState,
	ConcurrentChain blockHeaderChain,
	EventBus eventBus)
	: NodeBehavior
{
	private static readonly TimeSpan TickSyncInterval = TimeSpan.FromSeconds(2);

	private readonly List<CompactFilterPayload> _collectedFilters = [];

	private PageAssignment? _assignedHeaderPage;
	private PageAssignment? _assignedFilterPage;

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

		if (node.PeerVersion?.Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS) != true)
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

		if (_assignedHeaderPage is {} headerPageAssignment &&
		    message.Message.Payload is CompactFilterHeadersPayload {FilterType: FilterType.Basic} cfHeaders)
		{
			HandleFilterHeaderMessage(node, cfHeaders, headerPageAssignment);
			return;
		}

		if (_assignedFilterPage is {} filterPageAssignment &&
		    message.Message.Payload is CompactFilterPayload {FilterType: FilterType.Basic} filterPayload)
		{
			HandleFilterMessage(node, filterPayload, filterPageAssignment);
		}
	}

	private void HandleFilterHeaderMessage(Node node, CompactFilterHeadersPayload cfHeaders, PageAssignment assignment)
	{
		var filterHashes = cfHeaders.FilterHeaders;
		var batchCount = filterHashes.Count;

		Logger.LogDebug(
			$"Received {batchCount} filter headers from {node.Peer.Endpoint} for page {assignment.StartHeight}-{assignment.StopHeight}");

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
				$"Validation failed for filter header page {assignment.StartHeight}-{assignment.StopHeight}");
			HandleInvalid(node, "Invalid compact filter headers received");
			return;
		}

		Logger.LogInfo($"Successfully validated filter header page {assignment.StartHeight}-{assignment.StopHeight}");

		// Report success to shared state
		synchronizationState.OnHeaderPageCompleted(assignment.StartHeight, validatedHeaders);
		_assignedHeaderPage = null;

		// Immediately try to fetch the next page
		TrySync(node);
	}

	private void HandleFilterMessage(Node node, CompactFilterPayload filterPayload, PageAssignment assignment)
	{
		_collectedFilters.Add(filterPayload);

		// Check if we've received all filters for this page
		if (filterPayload.BlockHash != assignment.StopHash && _collectedFilters.Count < assignment.Count)
		{
			return;
		}

		Logger.LogDebug(
			$"Page {assignment.StartHeight}-{assignment.StopHeight} complete ({_collectedFilters.Count}/{assignment.Count} filters)");

		var filters = _collectedFilters.ToArray();
		_collectedFilters.Clear();

		// Validate all filters
		var validatedFilters = ValidateFilters(assignment.StartHeight, filters);

		if (validatedFilters is null)
		{
			// Validation failed - disconnect and release assignment
			Logger.LogWarning($"Validation failed for page {assignment.StartHeight}-{assignment.StopHeight}");
			HandleInvalid(node, "Invalid compact filters received");
			return;
		}

		Logger.LogInfo($"Successfully validated page {assignment.StartHeight}-{assignment.StopHeight}");

		// Report success
		synchronizationState.OnFilterPageCompleted(assignment.StartHeight, validatedFilters);
		_assignedFilterPage = null;

		// Immediately try to fetch the next page
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
		if (_assignedFilterPage is not null)
		{
			return;
		}

		if (!synchronizationState.TryAssignFilterPage(out var filterAssignment))
		{
			return;
		}

		_assignedFilterPage = filterAssignment;
		Logger.LogDebug(
			$"Assigned page {filterAssignment.StartHeight}-{filterAssignment.StopHeight} to node {node.Peer.Endpoint}");

		_collectedFilters.Clear();

		var payload = new GetCompactFiltersPayload(FilterType.Basic, filterAssignment.StartHeight,
			filterAssignment.StopHash);
		node.SendMessage(payload);
	}

	private void TrySyncHeaders(Node node)
	{
		if (_assignedHeaderPage is not null)
		{
			return;
		}

		if (!synchronizationState.TryAssignHeaderPage(out var headerAssignment))
		{
			return;
		}

		_assignedHeaderPage = headerAssignment;
		Logger.LogDebug($"Assigned filter header page {headerAssignment.StartHeight}-{headerAssignment.StopHeight} to node {node.Peer.Endpoint}");

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
		if (_assignedHeaderPage is not null)
		{
			synchronizationState.OnHeaderNodeDisconnected(_assignedHeaderPage);
			_assignedHeaderPage = null;
		}

		if (_assignedFilterPage is not null)
		{
			synchronizationState.OnFilterNodeDisconnected(_assignedFilterPage);
			_assignedFilterPage = null;
		}

		_collectedFilters.Clear();
	}

	private bool IsNodeInValidState(Node node)
	{
		if (_invalidReceived)
		{
			return false;
		}

		if (node is not {State: NodeState.HandShaked})
		{
			return false;
		}

		return true;
	}
}

public class FilterSynchronizationState
{
	private static readonly TimeSpan HeaderAssignmentTimeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan FilterAssignmentTimeout = TimeSpan.FromSeconds(60);
	private const int HeaderPageSize = 1_000;
	private const int FilterPageSize = 400;

	private readonly Lock _lock = new();
	private readonly ConcurrentChain _blockHeaderChain;
	private readonly FilterHeaderChain _filterHeaderChain;
	private readonly TimeProvider _timeProvider;

	private readonly PageAssignmentTracker<HeaderPage> _headerTracker;
	private readonly PageAssignmentTracker<FilterPage> _filterTracker;


	// Currently assigned filter fetches: pageStartHeight -> in-progress

	// The filter page currently waiting to be consumed by the Synchronizer (null if none)
	private FilterPage? _readyFilterPage;

	// Signal for when a new filter page becomes ready
	private TaskCompletionSource _filterPageReadySignal = new();

	public FilterSynchronizationState(ConcurrentChain blockHeaderChain, FilterHeaderChain filterHeaderChain,
		TimeProvider? timeProvider = null)
	{
		_blockHeaderChain = blockHeaderChain;
		_filterHeaderChain = filterHeaderChain;
		_timeProvider = timeProvider ?? TimeProvider.System;

		var initialHeaderHeight = _filterHeaderChain.Tip?.Height ?? 0;
		_headerTracker = new PageAssignmentTracker<HeaderPage>(initialHeaderHeight, _timeProvider);
		_filterTracker = new PageAssignmentTracker<FilterPage>(0, _timeProvider);
	}

	public FilterSynchronizationState(ConcurrentChain blockHeaders, FilterHeaderChain filterHeaders,
		ChainHeight tipHeight, TimeProvider? timeProvider = null)
		: this(blockHeaders, filterHeaders, timeProvider)
	{
		_filterTracker = new PageAssignmentTracker<FilterPage>(tipHeight, _timeProvider);
	}

	#region Filter Header Methods

	public bool TryAssignHeaderPage([NotNullWhen(true)] out PageAssignment? assignment)
	{
		assignment = null;
		lock (_lock)
		{
			// Check for and release any stale header assignments
			if (_headerTracker.GetOldestStaleAssignment(HeaderAssignmentTimeout) is { } staleHeaderHeight)
			{
				_headerTracker.RemoveActiveAssignment(staleHeaderHeight);
				Logger.LogWarning(
					$"Auto-released stale filter header assignment at height {staleHeaderHeight} (timeout: 60s)");
			}

			var chainTip = _blockHeaderChain.Tip;

			// Find the next page to assign
			var nextPageStart = _headerTracker.GetNextPageStartHeight(HeaderPageSize);

			// Nothing to fetch if we're caught up
			if (nextPageStart > chainTip.Height)
			{
				return false;
			}

			// Calculate stop height (limited by chain tip and page size)
			var stopHeight = (uint) Math.Min(nextPageStart + HeaderPageSize - 1, chainTip.Height);

			// Get the stop block hash
			var stopBlock = _blockHeaderChain.GetBlock((int) stopHeight);

			// Capture the expected previous filter header for this page
			var expectedPreviousFilterHeader = nextPageStart == 1
				? uint256.Zero
				: _filterHeaderChain[nextPageStart - 1]?.BlockFilterHeader;

			// Don't assign this page if we don't have the previous filter header yet
			if (expectedPreviousFilterHeader is null)
			{
				return false;
			}

			// Track this assignment so other nodes don't get the same page
			_headerTracker.AddActiveAssignment(nextPageStart);

			assignment = new PageAssignment(nextPageStart, stopHeight, stopBlock.HashBlock);
			return true;
		}
	}

	public void OnHeaderPageCompleted(uint pageStartHeight, SmartHeader[] headers)
	{
		lock (_lock)
		{
			// Remove from active assignments
			_headerTracker.MoveActiveToPendingAssignment(pageStartHeight, new HeaderPage(pageStartHeight, headers));

			// Process any pages that are now ready
			ProcessPendingHeaderPages();
		}
	}

	public void OnHeaderNodeDisconnected(PageAssignment assignedPage)
	{
		lock (_lock)
		{
			_headerTracker.RemoveActiveAssignment(assignedPage.StartHeight);
		}

		Logger.LogDebug($"Node disconnected, released filter header page {assignedPage.StartHeight} for reassignment");
	}

	public SmartHeader[]? ValidateFilterHeaders(
		PageAssignment assignment,
		uint256[] filterHashes,
		uint256 declaredPreviousFilterHeader)
	{
		// Recalculate the expected previous filter header from the chain
		var expectedPreviousFilterHeader = assignment.StartHeight == 1
			? uint256.Zero
			: _filterHeaderChain[assignment.StartHeight - 1]?.BlockFilterHeader;

		if (expectedPreviousFilterHeader is null)
		{
			Logger.LogWarning($"Cannot validate: previous filter header not available for page {assignment.StartHeight}");
			return null;
		}

		// Validate against the expected previous filter header
		if (declaredPreviousFilterHeader != expectedPreviousFilterHeader)
		{
			Logger.LogWarning(
				$"Previous filter header mismatch for page {assignment.StartHeight} - expected {expectedPreviousFilterHeader}, received {declaredPreviousFilterHeader}");
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

	private void ProcessPendingHeaderPages()
	{
		// Process pages in order
		while (true)
		{
			var nextExpectedStart = _headerTracker.LastHeight + 1;

			if (!_headerTracker.TryRemovePendingPage(nextExpectedStart, out var pendingPage))
			{
				// Next page not available yet
				break;
			}

			// Append all headers from this page
			foreach (var header in pendingPage.Headers)
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
				$"Successfully processed filter header page {pendingPage.StartHeight}, new tip at height {_headerTracker.LastHeight}");
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

	#endregion


	public async IAsyncEnumerable<FilterModel> GetNextPageFiltersAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var page = await WaitForReadyPageAsync(cancellationToken).ConfigureAwait(false);

		Logger.LogDebug($"Starting to yield {page.Filters.Length} filters from page {page.StartHeight}");

		foreach (var filter in page.Filters)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return filter;
		}

		var lastFilter = page.Filters[^1];
		AcknowledgeFilterPageConsumed(lastFilter.Header.Height);
	}

	private async ValueTask<FilterPage> WaitForReadyPageAsync(CancellationToken cancellationToken)
	{
		while (true)
		{
			Task waitTask;

			lock (_lock)
			{
				if (_readyFilterPage is { } page)
				{
					return page;
				}

				waitTask = _filterPageReadySignal.Task;

				Logger.LogTrace($"No page ready yet - pending pages: {_filterTracker.PendingCount}, active assignments: {_filterTracker.ActiveCount}");
			}

			await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public bool TryAssignFilterPage([NotNullWhen(true)] out PageAssignment? assignment)
	{
		assignment = null;
		lock (_lock)
		{
			// Check for and release any stale filter assignments (older than 60 seconds)
			if (_filterTracker.GetOldestStaleAssignment(FilterAssignmentTimeout) is { } staleFilterHeight)
			{
				_filterTracker.RemoveActiveAssignment(staleFilterHeight);
				Logger.LogWarning(
					$"Auto-released stale filter assignment at height {staleFilterHeight} (timeout: 60s)");
			}

			// Check if filter headers are synced ahead
			var filterHeadersTip = _filterHeaderChain.Tip!;

			// Find the next page to assign
			var nextPageStart = _filterTracker.GetNextPageStartHeight(FilterPageSize);

			// Nothing to fetch if we're caught up with filter headers
			if (nextPageStart > filterHeadersTip.Height)
			{
				Logger.LogTrace(
					$"TryAssignFilterPage - caught up (next page start {nextPageStart} > filter headers tip {filterHeadersTip.Height})");
				return false;
			}

			// Calculate stop height (limited by filter headers tip and page size)
			var stopHeight = Math.Min(nextPageStart + FilterPageSize - 1, filterHeadersTip.Height);

			// Get the stop block hash
			var stopBlock = _blockHeaderChain.GetBlock((int) stopHeight);

			// Track this assignment so other nodes don't get the same page
			_filterTracker.AddActiveAssignment(nextPageStart);

			Logger.LogDebug(
				$"Assigned filter page {nextPageStart}-{stopHeight} (active: {_filterTracker.ActiveCount}, pending: {_filterTracker.PendingCount})");

			assignment = new PageAssignment(nextPageStart, stopHeight, stopBlock.HashBlock);
			return true;
		}
	}

	public void OnFilterPageCompleted(uint pageStartHeight, FilterModel[] filters)
	{
		lock (_lock)
		{
			_filterTracker.MoveActiveToPendingAssignment(pageStartHeight, new FilterPage(pageStartHeight, filters));

			Logger.LogTrace(
				$"State after filter page completion - active: {_filterTracker.ActiveCount}, pending: {_filterTracker.PendingCount}, next expected: {_filterTracker.LastHeight + 1}");

			// Try to make the next page ready for consumption
			TryMakeNextFilterPageReady();
		}
	}

	public void OnFilterNodeDisconnected(PageAssignment assignedFilterPage)
	{
		lock (_lock)
		{
			_filterTracker.RemoveActiveAssignment(assignedFilterPage.StartHeight);
		}

		Logger.LogDebug($"Node disconnected, released filter page {assignedFilterPage.StartHeight} for reassignment");
	}

	public uint256? GetExpectedFilterHeader(uint height)
	{
		return _filterHeaderChain[height]?.BlockFilterHeader;
	}

	private void AcknowledgeFilterPageConsumed(uint pageEndHeight)
	{
		lock (_lock)
		{
			if (_readyFilterPage is null)
			{
				return;
			}

			_filterTracker.SetLastHeight(pageEndHeight);
			_readyFilterPage = null;

			// Reset the signal for the next page
			_filterPageReadySignal = new TaskCompletionSource();

			Logger.LogDebug($"Filter page consumed, now at height {_filterTracker.LastHeight}");

			// Try to make the next page ready
			TryMakeNextFilterPageReady();
		}
	}

	private void TryMakeNextFilterPageReady()
	{
		// Don't make a new page ready if one is already waiting
		if (_readyFilterPage is not null)
		{
			Logger.LogTrace(
				$"TryMakeNextFilterPageReady - page {_readyFilterPage.StartHeight} already ready, not making new page ready");
			return;
		}

		var nextExpectedStart = _filterTracker.LastHeight + 1;

		if (!_filterTracker.TryRemovePendingPage(nextExpectedStart, out var pendingPage))
		{
			// Next page not available yet
			if (_filterTracker.HasPendingPages)
			{
				var pendingHeights = string.Join(", ", _filterTracker.GetPendingPageHeights());
				Logger.LogTrace(
					$"Next expected filter page {nextExpectedStart} not available yet. Pending pages at heights: {pendingHeights}");
			}
			else
			{
				Logger.LogTrace($"Next expected filter page {nextExpectedStart} not available yet. No pending pages.");
			}

			return;
		}

		_readyFilterPage = pendingPage;

		Logger.LogDebug(
			$"Filter page {pendingPage.StartHeight} ready for consumption ({pendingPage.Filters.Length} filters)");

		// Signal that a page is ready
		_filterPageReadySignal.TrySetResult();
	}
}

public record PageAssignment(uint StartHeight, uint StopHeight, uint256 StopHash)
{
	public uint Count => StopHeight - StartHeight + 1;
}

public abstract record Page(uint StartHeight);

public record HeaderPage(uint StartHeight, SmartHeader[] Headers) : Page(StartHeight);

public record FilterPage(uint StartHeight, FilterModel[] Filters) : Page(StartHeight);
