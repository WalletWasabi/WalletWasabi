using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using static WalletWasabi.BitcoinP2p.FilterSynchronizationState;

namespace WalletWasabi.BitcoinP2p;

public partial class CompactFilterBehavior(
	FilterSynchronizationState synchronizationState,
	ConcurrentChain blockHeaderChain,
	EventBus eventBus)
	: NodeBehavior
{
	private static readonly TimeSpan TickSyncInterval = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan HeaderAssignmentTimeoutForClearnet = TimeSpan.FromSeconds(25);
	private static readonly TimeSpan FilterAssignmentTimeoutForClearnet = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan HeaderAssignmentTimeoutForTor = TimeSpan.FromSeconds(45);
	private static readonly TimeSpan FilterAssignmentTimeoutForTor = TimeSpan.FromSeconds(70);

	private TimeSpan _headerAssignmentTimeout = HeaderAssignmentTimeoutForTor;
	private TimeSpan _filterAssignmentTimeout = FilterAssignmentTimeoutForTor;

	private readonly Lock _lock = new();
	private readonly List<CompactFilterPayload> _collectedFilters = [];

	private RangeRequest? _assignedHeaderRange;
	private DateTime _assignedHeaderRangeAt;
	private RangeRequest? _assignedFilterRange;
	private DateTime _assignedFilterRangeAt;

	private volatile bool _invalidReceived;

	protected override void AttachCore()
	{
		AttachedNode.StateChanged += OnStateChanged;
		AttachedNode.MessageReceived += OnMessageReceived;
		if (AttachedNode.Behaviors.Find<SocksSettingsBehavior>() is null)
		{
			_headerAssignmentTimeout = HeaderAssignmentTimeoutForClearnet;
			_filterAssignmentTimeout = FilterAssignmentTimeoutForClearnet;
		}

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

			// Check for stale assignments - disconnect if timed out
			if (CheckAndHandleStaleAssignment(node, nowUtc))
			{
				return;
			}

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

		lock (_lock)
		{
			if (_assignedHeaderRange is { } assignedHeaderRange &&
			    message.Message.Payload is CompactFilterHeadersPayload {FilterType: FilterType.Basic} cfHeaders)
			{
				HandleFilterHeaderMessageNoLock(node, cfHeaders, assignedHeaderRange);
				return;
			}

			if (_assignedFilterRange is { } assignedFilterRange &&
			    message.Message.Payload is CompactFilterPayload {FilterType: FilterType.Basic} filterPayload)
			{
				HandleFilterMessageNoLock(node, filterPayload, assignedFilterRange);
			}
		}
	}

	private void HandleFilterHeaderMessageNoLock(Node node, CompactFilterHeadersPayload cfHeaders, RangeRequest assignment)
	{
		var filterHashes = cfHeaders.FilterHeaders;
		var batchCount = filterHashes.Count;

		Logger.LogDebug(
			$"Received {batchCount} filter headers from {node.Peer.Endpoint} for range {assignment}");

		if (batchCount == 0)
		{
			Logger.LogDebug("Received empty cfheaders batch");
			HandleInvalidNoLock(node, "Invalid compact filter headers received");
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
			HandleInvalidNoLock(node, "Invalid compact filter headers received");
			return;
		}

		// Verify this matches our assignment
		if ((uint) startHeight != assignment.StartHeight)
		{
			Logger.LogWarning(
				$"Received headers for wrong range - expected {assignment.StartHeight}, got {startHeight}");
			HandleInvalidNoLock(node, "Invalid compact filter headers received");
			return;
		}

		// Validate headers using shared state
		var filterHashArray = filterHashes.ToArray();
		var validationResult = synchronizationState.ValidateFilterHeaders(
			assignment,
			filterHashArray,
			cfHeaders.PreviousFilterHeader,
			node.Network);

		switch (validationResult)
		{
			case HeaderValidationResult.Success:
				Logger.LogInfo($"Successfully validated filter header range {assignment}");
				_assignedHeaderRange = null;
				TrySyncNoLock(node);
				break;

			case HeaderValidationResult.NotReadyYet:
				Logger.LogDebug($"Buffering filter header range {assignment} for later validation");
				_assignedHeaderRange = null;
				TrySyncNoLock(node);
				break;

			case HeaderValidationResult.Invalid invalid:
				Logger.LogWarning($"Validation failed for filter header range {assignment}: {invalid.Reason}");
				HandleInvalidNoLock(node, "Invalid compact filter headers received");
				break;
		}
	}

	private void HandleFilterMessageNoLock(Node node, CompactFilterPayload filterPayload, RangeRequest assignment)
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
		var validatedFilters = ValidateFilters(assignment.StartHeight, filters, node.Network);

		if (validatedFilters is null)
		{
			// Validation failed - disconnect and release assignment
			Logger.LogWarning($"Validation failed for range {assignment}");
			HandleInvalidNoLock(node, "Invalid compact filters received");
			return;
		}

		Logger.LogInfo($"Successfully validated range {assignment}");

		// Report success
		synchronizationState.OnFilterRangeCompleted(assignment.StartHeight, validatedFilters);
		_assignedFilterRange = null;

		// Immediately try to fetch the next range
		TrySyncNoLock(node);
	}

	private FilterModel[]? ValidateFilters(uint startHeight, CompactFilterPayload[] filters, Network network)
	{
		if (filters.Length == 0)
		{
			Logger.LogWarning("Received empty filter batch");
			return null;
		}

		var result = new FilterModel[filters.Length];

		// For the first filter, we need the previous filter header
		// This comes from either the shared state (last emitted) or the filter header chain
		if (!synchronizationState.TryGetPreviousFilterHeader(startHeight, network, out var prevFilterHeader))
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

		lock (_lock)
		{
			TrySyncHeadersNoLock(node);
			TrySyncFiltersNoLock(node);
		}
	}

	private void TrySyncNoLock(Node node)
	{
		if (!IsNodeInValidState(node))
		{
			return;
		}

		TrySyncHeadersNoLock(node);
		TrySyncFiltersNoLock(node);
	}

	private void TrySyncFiltersNoLock(Node node)
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
		_assignedFilterRangeAt = DateTime.UtcNow;
		Logger.LogDebug(
			$"Assigned range {filterAssignment} to node {node.Peer.Endpoint}");

		_collectedFilters.Clear();

		var payload = new GetCompactFiltersPayload(FilterType.Basic, filterAssignment.StartHeight,
			filterAssignment.StopHash);
		node.SendMessage(payload);
	}

	private void TrySyncHeadersNoLock(Node node)
	{
		if (_assignedHeaderRange is not null)
		{
			return;
		}

		if (!synchronizationState.TryAssignHeaderRange(node.Network, out var headerAssignment))
		{
			return;
		}

		_assignedHeaderRange = headerAssignment;
		_assignedHeaderRangeAt = DateTime.UtcNow;
		Logger.LogDebug($"Assigned filter header range {headerAssignment} to node {node.Peer.Endpoint}");

		var payload = new GetCompactFilterHeadersPayload(FilterType.Basic, headerAssignment.StartHeight,
			headerAssignment.StopHash);
		node.SendMessage(payload);
	}

	private void HandleInvalidNoLock(Node node, string reason)
	{
		_invalidReceived = true;

		ReleaseAssignmentsNoLock();

		Logger.LogWarning($"Disconnecting node {node.Peer.Endpoint}: {reason}");

		// Disconnect the node
		node.DisconnectAsync(reason);
	}

	private void ReleaseAssignments()
	{
		lock (_lock)
		{
			ReleaseAssignmentsNoLock();
		}
	}

	private void ReleaseAssignmentsNoLock()
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

	private bool CheckAndHandleStaleAssignment(Node node, DateTime nowUtc)
	{
		lock (_lock)
		{
			// Check filter assignment timeout
			if (_assignedFilterRange is not null)
			{
				var elapsed = nowUtc - _assignedFilterRangeAt;
				if (elapsed > _filterAssignmentTimeout)
				{
					HandleInvalidNoLock(node, $"Filter assignment at {_assignedFilterRange.StartHeight} timed out after {elapsed.TotalSeconds:F1}s, disconnecting {node.Peer.Endpoint}");
					return true;
				}
			}

			// Check header assignment timeout
			if (_assignedHeaderRange is not null)
			{
				var elapsed = nowUtc - _assignedHeaderRangeAt;
				if (elapsed > _headerAssignmentTimeout)
				{
					HandleInvalidNoLock(node, $"Header assignment at {_assignedHeaderRange.StartHeight} timed out after {elapsed.TotalSeconds:F1}s, disconnecting {node.Peer.Endpoint}");
					return true;
				}
			}

			return false;
		}
	}

	private bool IsNodeInValidState(Node node)
	{
		if (_invalidReceived)
		{
			return false;
		}

		return node is {State: NodeState.HandShaked};
	}
}
