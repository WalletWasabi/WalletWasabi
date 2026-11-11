using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Reflection.PortableExecutable;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using static WalletWasabi.BitcoinP2p.FilterSynchronizationState;

namespace WalletWasabi.BitcoinP2p;

public class CompactFilterBehavior(
	FilterSynchronizationState synchronizationState,
	ConcurrentChain blockHeaderChain,
	EventBus eventBus)
	: NodeBehavior
{
	private static readonly TimeSpan TickSyncInterval = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan HeaderAssignmentTimeoutForClearnet = TimeSpan.FromSeconds(25);
	private static readonly TimeSpan FilterProgressTimeoutForClearnet = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan HeaderAssignmentTimeoutForTor = TimeSpan.FromSeconds(45);
	private static readonly TimeSpan FilterProgressTimeoutForTor = TimeSpan.FromSeconds(70);
	private static readonly TimeSpan FilterAssignmentTimeout = TimeSpan.FromMinutes(10);

	private TimeSpan _headerAssignmentTimeout = HeaderAssignmentTimeoutForTor;
	private TimeSpan _filterProgressTimeout = FilterProgressTimeoutForTor;

	private abstract record Assignment(RangeRequest RangeRequest, DateTime AssignedAt)
	{
		public long BytesReceived { get; private set; }

		public void CountReceivedBytes(long bytes) => BytesReceived += bytes;

		public string GetSpeed()
		{
			var seconds = Math.Max((DateTime.UtcNow - AssignedAt).TotalSeconds, 0.001);
			return $"{BytesReceived / 1024.0 / seconds:F1} KB/s; {BytesReceived / 1024.0:F1} KB in {seconds:F1}s";
		}
	}

	private sealed record HeaderAssignment(RangeRequest RangeRequest, DateTime AssignedAt)
		: Assignment(RangeRequest, AssignedAt);

	private sealed record FilterAssignment(RangeRequest RangeRequest, DateTime AssignedAt)
		: Assignment(RangeRequest, AssignedAt)
	{
		/// <summary>Filters received so far for the assigned range.</summary>
		public List<CompactFilterPayload> CollectedFilters { get; } = [];

		/// <summary>When the last filter of the assigned range arrived.</summary>
		public DateTime LastProgressAt { get; set; } = AssignedAt;

		public void AddFilter(CompactFilterPayload filter)
		{
			CollectedFilters.Add(filter);
			LastProgressAt = DateTime.UtcNow;
			CountReceivedBytes(filter.FilterBytes.Length);
		}

		public string GetProgress() => $"{CollectedFilters.Count}/{RangeRequest.Count}";
	}

	private readonly Lock _lock = new();

	/// <summary>Assignment of the node to download headers/filters from the node, or <c>null</c> if there is none at the moment.</summary>
	private Assignment? _assignment;

	private volatile bool _invalidReceived;

	protected override void AttachCore()
	{
		AttachedNode.StateChanged += OnStateChanged;
		AttachedNode.MessageReceived += OnMessageReceived;
		if (AttachedNode.Behaviors.Find<SocksSettingsBehavior>() is null)
		{
			_headerAssignmentTimeout = HeaderAssignmentTimeoutForClearnet;
			_filterProgressTimeout = FilterProgressTimeoutForClearnet;
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
			switch (_assignment)
			{
				case HeaderAssignment headerAssignment when message.Message.Payload is CompactFilterHeadersPayload { FilterType: FilterType.Basic } cfHeaders:
					HandleFilterHeaderMessageNoLock(node, cfHeaders, headerAssignment);
					break;

				case FilterAssignment filterAssignment when message.Message.Payload is CompactFilterPayload { FilterType: FilterType.Basic } filterPayload:
					HandleFilterMessageNoLock(node, filterPayload, filterAssignment);
					break;
			}
		}
	}

	private void HandleFilterHeaderMessageNoLock(Node node, CompactFilterHeadersPayload cfHeaders, HeaderAssignment assignment)
	{
		var filterHashes = cfHeaders.FilterHeaders;
		var batchCount = filterHashes.Count;

		Logger.LogDebug($"Received {batchCount} filter headers from {node.Peer.Endpoint} for range {assignment.RangeRequest}");

		if (batchCount == 0)
		{
			Logger.LogDebug("Received an empty compact filter headers batch.");
			HandleInvalidNoLock(node, "Invalid compact filter headers received");
			return;
		}

		assignment.CountReceivedBytes(batchCount * 32L);

		// Resolve the stop block to validate the response
		var stopBlock = blockHeaderChain.GetBlock(cfHeaders.StopHash);
		if (stopBlock == null)
		{
			return;
		}

		var startHeight = stopBlock.Height - batchCount + 1;
		if (startHeight < 0)
		{
			Logger.LogWarning($"Invalid batch - start height {startHeight} is negative (stopHeight={stopBlock.Height}, batchCount={batchCount})");
			HandleInvalidNoLock(node, "Invalid compact filter headers received");
			return;
		}

		// Verify this matches our assignment
		if ((uint) startHeight != assignment.RangeRequest.StartHeight)
		{
			Logger.LogWarning($"Received headers for wrong range - expected {assignment.RangeRequest.StartHeight}, got {startHeight}");
			HandleInvalidNoLock(node, "Invalid compact filter headers received");
			return;
		}

		// Validate headers using shared state
		var filterHashArray = filterHashes.ToArray();
		var validationResult = synchronizationState.ValidateFilterHeaders(
			assignment.RangeRequest,
			filterHashArray,
			cfHeaders.PreviousFilterHeader,
			node.Network);

		switch (validationResult)
		{
			case HeaderValidationResult.Success:
				Logger.LogInfo($"Successfully validated filter header range {assignment.RangeRequest}");
				_assignment = null;
				TrySyncNoLock(node);
				break;

			case HeaderValidationResult.NotReadyYet:
				Logger.LogDebug($"Buffering filter header range {assignment.RangeRequest} for later validation");
				_assignment = null;
				TrySyncNoLock(node);
				break;

			case HeaderValidationResult.Invalid invalid:
				Logger.LogWarning($"Validation failed for filter header range {assignment.RangeRequest}: {invalid.Reason}");
				HandleInvalidNoLock(node, "Invalid compact filter headers received");
				break;
		}
	}

	private void HandleFilterMessageNoLock(Node node, CompactFilterPayload filterPayload, FilterAssignment assignment)
	{
		const int MaxFilterBytes = 1_000_000;

		if (filterPayload.FilterBytes.Length > MaxFilterBytes)
		{
			Logger.LogWarning($"Filter too large: {filterPayload.FilterBytes.Length} bytes");
			HandleInvalidNoLock(node, "Filter exceeds maximum size");
			return;
		}

		var filterResult = Result<GolombRiceFilter, Exception>.Catch(() => new GolombRiceFilter(filterPayload.FilterBytes));
		if (!filterResult.IsOk)
		{
			Logger.LogWarning($"Malformed Golomb-Rice filter: {filterResult.Error.Message}");
			HandleInvalidNoLock(node, "Invalid compact filter received");
			return;
		}

		var filter = filterResult.Value;

		// Reject degenerate filters: N=0 with data present
		if (filter.N == 0 && filter.Data.Length > 0)
		{
			Logger.LogWarning("Invalid filter: N=0 with non-empty data");
			HandleInvalidNoLock(node, "Invalid compact filter received");
			return;
		}

		assignment.AddFilter(filterPayload);

		// Check if we've received all filters for this range
		if (filterPayload.BlockHash != assignment.RangeRequest.StopHash && assignment.CollectedFilters.Count < assignment.RangeRequest.Count)
		{
			return;
		}

		Logger.LogDebug($"Filter assignment {assignment.RangeRequest} is complete ({assignment.GetProgress()})");
		var validatedFilters = ValidateFilters(assignment.RangeRequest.StartHeight, assignment.CollectedFilters, node.Network);

		if (validatedFilters is null)
		{
			// Validation failed - disconnect and release assignment
			Logger.LogWarning($"Validation failed for range {assignment.RangeRequest}");
			HandleInvalidNoLock(node, "Invalid compact filters received");
			return;
		}

		Logger.LogInfo($"Successfully validated range {assignment.RangeRequest} ({assignment.GetSpeed()})");

		// Report success
		synchronizationState.OnFilterRangeCompleted(assignment.RangeRequest.StartHeight, validatedFilters);
		_assignment = null;

		// Immediately try to fetch the next range
		TrySyncNoLock(node);
	}

	internal FilterModel[]? ValidateFilters(uint startHeight, IReadOnlyList<CompactFilterPayload> filters, Network network)
	{
		if (filters.Count == 0)
		{
			Logger.LogWarning("Received empty filter batch");
			return null;
		}

		var result = new FilterModel[filters.Count];

		// For the first filter, we need the previous filter header
		// This comes from either the shared state (last emitted) or the filter header chain
		if (!synchronizationState.TryGetPreviousFilterHeader(startHeight, network, out var prevFilterHeader))
		{
			Logger.LogWarning($"Cannot validate filters: previous filter header not available for height {startHeight}");
			return null;
		}

		for (var i = 0; i < filters.Count; i++)
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
				Logger.LogWarning($"Invalid filter at height {height}: expected header {expectedHeader}, got {actualHeader}");
				return null;
			}

			// Get block info for the SmartHeader
			var block = blockHeaderChain.GetBlock((int) height);
			if (block is null)
			{
				Logger.LogWarning($"Block header not available for height {height}");
				return null;
			}

			if (filterPayload.BlockHash != block.HashBlock)
			{
				Logger.LogWarning($"The filter's block hash {filterPayload.BlockHash} doesn't match the expected {block.HashBlock} at height {height}");
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
		lock (_lock)
		{
			TrySyncNoLock(node);
		}
	}

	private void TrySyncNoLock(Node node)
	{
		if (!IsNodeInValidState(node))
		{
			return;
		}

		if (_assignment is null)
		{
			TrySyncHeadersNoLock(node);
		}

		if (_assignment is null)
		{
			TrySyncFiltersNoLock(node);
		}
	}

	private void TrySyncFiltersNoLock(Node node)
	{
		if (!synchronizationState.TryAssignFilterRange(out var filterAssignment, out var activeCount, out var pendingCount))
		{
			return;
		}

		_assignment = new FilterAssignment(filterAssignment, DateTime.UtcNow);
		Logger.LogDebug($"Assigned compact filter range {filterAssignment} (active: {activeCount}, pending: {pendingCount}) to node {node.Peer.Endpoint}");

		var payload = new GetCompactFiltersPayload(FilterType.Basic, filterAssignment.StartHeight, filterAssignment.StopHash);
		node.SendMessage(payload);
	}

	private void TrySyncHeadersNoLock(Node node)
	{
		if (!synchronizationState.TryAssignHeaderRange(out var range))
		{
			return;
		}

		_assignment = new HeaderAssignment(range, DateTime.UtcNow);
		Logger.LogDebug($"Assigned filter header range {range} to node {node.Peer.Endpoint}");

		var payload = new GetCompactFilterHeadersPayload(FilterType.Basic, range.StartHeight, range.StopHash);
		node.SendMessage(payload);
	}

	private void HandleInvalidNoLock(Node node, string reason)
	{
		_invalidReceived = true;
		ReleaseAssignmentsNoLock();

		Logger.LogWarning($"Disconnecting node {node.Peer.Endpoint}: {reason}");
		node.DisconnectAsync(reason); // Not async method, just async process.
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
		switch (_assignment)
		{
			case HeaderAssignment assignment:
				Logger.LogDebug($"Releasing header assignment {assignment.RangeRequest} ({assignment.GetSpeed()})");
				synchronizationState.OnHeaderNodeDisconnected(assignment.RangeRequest);
				break;

			case FilterAssignment assignment:
				Logger.LogDebug($"Releasing filter assignment {assignment.RangeRequest} ({assignment.GetSpeed()})");
				synchronizationState.OnFilterNodeDisconnected(assignment.RangeRequest);
				break;
		}

		_assignment = null;
	}

	/// <summary>
	/// Check if the current header/filter assignment has timed out or stalled, and if so, handle it by disconnecting the node.
	/// </summary>
	private bool CheckAndHandleStaleAssignment(Node node, DateTime nowUtc)
	{
		lock (_lock)
		{
			if (_assignment is null)
			{
				return false;
			}

			var assignmentDuration = nowUtc - _assignment.AssignedAt;

			// For header assignments, we only check if the timeout elapsed or not.
			switch (_assignment)
			{
				case HeaderAssignment assignment:
					if (assignmentDuration > _headerAssignmentTimeout)
					{
						HandleInvalidNoLock(node, $"Header assignment {assignment.RangeRequest} timed out after {assignmentDuration.TotalSeconds:F1}s.");
						return true;
					}
					break;

				case FilterAssignment assignment:
					// For filter assignments, we check the progress timeout and the overall assignment timeout.
					var sinceLastProgress = nowUtc - assignment.LastProgressAt;
					if (sinceLastProgress > _filterProgressTimeout)
					{
						var timeout = $"{_filterProgressTimeout.TotalSeconds:F1}s";
						var reason = $"Filter range {assignment.RangeRequest} ({assignment.GetProgress()}) reached the progress timeout of {timeout}. {assignment.GetSpeed()}.";
						HandleInvalidNoLock(node, reason);
						return true;
					}

					if (assignmentDuration > FilterAssignmentTimeout)
					{
						var timeout = $"{FilterAssignmentTimeout.TotalSeconds:F1}s";
						var reason = $"Filter range {assignment.RangeRequest} ({assignment.GetProgress()}) reached the maximum timeout of {timeout}. " +
							$"The last update was {sinceLastProgress.TotalSeconds:F1}s ago. {assignment.GetSpeed()}.";
						HandleInvalidNoLock(node, reason);
						return true;
					}
					break;
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
