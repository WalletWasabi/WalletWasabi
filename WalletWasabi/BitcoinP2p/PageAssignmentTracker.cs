using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.BitcoinP2p;

/// <summary>
/// Tracks page assignments for parallel filter synchronization.
/// Manages active fetches, pending completed pages, and sequential page height progression.
/// </summary>
/// <typeparam name="TPage">The type of validated page (ValidatedHeaderPage or ValidatedFilterPage)</typeparam>
public class PageAssignmentTracker<TPage>(uint initialHeight = 0, TimeProvider? timeProvider = null) where TPage : Page
{
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
	private readonly SortedDictionary<uint, TPage> _pendingPages = [];
	private readonly SortedDictionary<uint, DateTime> _activeAssignments = [];

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
	public int PendingCount => _pendingPages.Count;

	/// <summary>
	/// Marks a page as actively being fetched.
	/// </summary>
	public void AddActiveAssignment(uint startHeight)
	{
		_activeAssignments.Add(startHeight, _timeProvider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Removes an active assignment (on completion or node disconnect).
	/// </summary>
	public void RemoveActiveAssignment(uint startHeight)
	{
		_activeAssignments.Remove(startHeight);
	}

	public void MoveActiveToPendingAssignment(uint startHeight, TPage page)
	{
		// Remove from active assignments
		RemoveActiveAssignment(startHeight);
		_pendingPages[startHeight] = page;
	}

	/// <summary>
	/// Attempts to remove and return a pending page at the specified height.
	/// </summary>
	public bool TryRemovePendingPage(uint startHeight, [NotNullWhen(true)] out TPage? page)
	{
		if (_pendingPages.Remove(startHeight, out var removedPage))
		{
			page = removedPage;
			return true;
		}
		page = null;
		return false;
	}

	/// <summary>
	/// Gets all pending page heights (for logging).
	/// </summary>
	public IEnumerable<uint> GetPendingPageHeights()
	{
		return _pendingPages.Keys.OrderBy(k => k);
	}

	/// <summary>
	/// Checks if there are any pending pages.
	/// </summary>
	public bool HasPendingPages => _pendingPages.Count > 0;

	/// <summary>
	/// Updates the last processed/emitted height.
	/// </summary>
	public void SetLastHeight(uint height)
	{
		LastHeight = height;
	}

	/// <summary>
	/// Calculates the next available page start height, skipping active and pending pages.
	/// </summary>
	/// <param name="pageSize">The size of each page (e.g., 1000)</param>
	public uint GetNextPageStartHeight(uint pageSize)
	{
		var nextStart = LastHeight + 1;

		// Skip over any pages that are already assigned or pending
		while (_activeAssignments.ContainsKey(nextStart) || _pendingPages.ContainsKey(nextStart))
		{
			nextStart += pageSize;
		}

		return nextStart;
	}

	/// <summary>
	/// Finds the oldest active assignment that is older than the specified timeout.
	/// Returns null if no stale assignments found.
	/// </summary>
	public uint? GetOldestStaleAssignment(TimeSpan timeout)
	{
		var cutoffTime = _timeProvider.GetUtcNow().UtcDateTime - timeout;

		return _activeAssignments
			.Where(kvp => kvp.Value < cutoffTime)
			.OrderBy(kvp => kvp.Value)
			.Select(kvp => (uint?)kvp.Key)
			.FirstOrDefault();
	}
}
