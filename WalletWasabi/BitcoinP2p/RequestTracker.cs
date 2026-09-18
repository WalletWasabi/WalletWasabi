using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.BitcoinP2p;

public class RequestTracker<TProcessedResponse>(TimeProvider timeProvider, uint initialHeight = 0) where TProcessedResponse : Response
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
		=> TryGetNextRangeStartHeight(maxLookaheadRanges, null, out startHeight);

	public bool TryGetNextRangeStartHeight(int maxLookaheadRanges, IReadOnlyDictionary<uint, UnvalidatedHeaderResponse>? additionalSkip, out uint startHeight)
	{
		var nextStart = LastHeight + 1;
		var rangeCount = 0;

		// Skip over any ranges that are already assigned, pending, or in the additional skip set
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
			else if (additionalSkip is not null && additionalSkip.TryGetValue(nextStart, out var buffered))
			{
				nextStart = buffered.Assignment.StopHeight + 1;
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

	/// <summary>
	/// Drops all active and pending ranges and continues from <paramref name="height"/>.
	/// Used when the filter header chain was rolled back below <see cref="LastHeight"/>.
	/// </summary>
	public void RewindTo(uint height)
	{
		_pendingResponses.Clear();
		_activeAssignments.Clear();
		LastHeight = height;
	}
}

public abstract record Response(uint StartHeight)
{
	public abstract uint EndHeight { get; }
}

public record HeaderResponse(uint StartHeight, SmartHeader[] Headers) : Response(StartHeight)
{
	public override uint EndHeight => Headers[^1].Height;
}

public record FilterResponse(uint StartHeight, FilterModel[] Filters) : Response(StartHeight)
{
	public override uint EndHeight => Filters[^1].Header.Height;
}

/// <summary>
/// Stores raw filter header data received from a peer, awaiting validation
/// when the previous filter header becomes available.
/// </summary>
public record UnvalidatedHeaderResponse(
	RangeRequest Assignment,
	uint256[] FilterHashes,
	uint256 DeclaredPreviousFilterHeader,
	Network Network);


public record RangeRequest(uint StartHeight, uint StopHeight, uint256 StopHash)
{
	public uint Count => StopHeight - StartHeight + 1;
	public override string ToString() => $"{StartHeight}-{StopHeight}";
}
