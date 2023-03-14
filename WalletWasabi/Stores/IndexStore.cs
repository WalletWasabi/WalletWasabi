using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Nito.AsyncEx;

namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class IndexStore : IAsyncDisposable
{
	private int _throttleId;

	public IndexStore(string workFolderPath, Network network, SmartHeaderChain smartHeaderChain)
	{
		workFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(workFolderPath);
		var indexFilePath = Path.Combine(workFolderPath, "MatureIndex.dat");
		MatureIndexFileManager = new DigestableSafeIoManager(indexFilePath, useLastCharacterDigest: true);
		var immatureIndexFilePath = Path.Combine(workFolderPath, "ImmatureIndex.dat");
		ImmatureIndexFileManager = new DigestableSafeIoManager(immatureIndexFilePath, useLastCharacterDigest: true);

		Network = network;
		StartingFilter = StartingFilters.GetStartingFilter(Network);
		SmartHeaderChain = smartHeaderChain;
	}

	public event EventHandler<FilterModel>? Reorged;

	public event EventHandler<FilterModel>? NewFilter;

	private AbandonedTasks AbandonedTasks { get; } = new();

	private Network Network { get; }
	private DigestableSafeIoManager MatureIndexFileManager { get; }
	private DigestableSafeIoManager ImmatureIndexFileManager { get; }

	/// <summary>Lock for modifying <see cref="ImmatureFilters"/>. This should be lock #1.</summary>
	private AsyncLock IndexLock { get; } = new();

	/// <summary>Lock for accessing <see cref="MatureIndexFileManager"/>. This should be lock #2.</summary>
	private AsyncLock MatureIndexAsyncLock { get; } = new();

	/// <summary>Lock for accessing <see cref="ImmatureIndexFileManager"/>. This should be lock #3.</summary>
	private AsyncLock ImmatureIndexAsyncLock { get; } = new();

	public SmartHeaderChain SmartHeaderChain { get; }

	private FilterModel StartingFilter { get; }
	private uint StartingHeight => StartingFilter.Header.Height;
	private List<FilterModel> ImmatureFilters { get; } = new(150);

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		using IDisposable _ = BenchmarkLogger.Measure();

		using (await IndexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		using (await MatureIndexAsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		using (await ImmatureIndexAsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			if (Network == Network.RegTest)
			{
				MatureIndexFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
				ImmatureIndexFileManager.DeleteMe();
			}

			cancellationToken.ThrowIfCancellationRequested();

			if (!MatureIndexFileManager.Exists())
			{
				await MatureIndexFileManager.WriteAllLinesAsync(new[] { StartingFilter.ToLine() }, CancellationToken.None).ConfigureAwait(false);
			}

			cancellationToken.ThrowIfCancellationRequested();

			await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <remarks>Guarded by <see cref="IndexLock"/>, <see cref="MatureIndexAsyncLock"/> and <see cref="ImmatureIndexAsyncLock"/>.</remarks>
	private async Task InitializeFiltersNoLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (MatureIndexFileManager.Exists())
			{
				using (BenchmarkLogger.Measure(LogLevel.Debug, "MatureIndexFileManager loading"))
				{
					int i = 0;
					using StreamReader sr = MatureIndexFileManager.OpenText();

					if (!sr.EndOfStream)
					{
						while (true)
						{
							i++;
							cancellationToken.ThrowIfCancellationRequested();
							string? line = await sr.ReadLineAsync(CancellationToken.None).ConfigureAwait(false);

							if (line is null)
							{
								break;
							}

							ProcessLineNoLock(line, enqueue: false);
						}
					}

					Logger.LogDebug($"Loaded {i} lines from the mature index file.");
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// We found a corrupted entry. Stop here.
			// Delete the corrupted file.
			// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
			Logger.LogError("Mature index got corrupted. Deleting both mature and immature index...");
			MatureIndexFileManager.DeleteMe();
			ImmatureIndexFileManager.DeleteMe();
			throw;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (ImmatureIndexFileManager.Exists())
			{
				foreach (var line in await ImmatureIndexFileManager.ReadAllLinesAsync(cancellationToken).ConfigureAwait(false)) // We can load ImmatureIndexFileManager to the memory, no problem.
				{
					ProcessLineNoLock(line, enqueue: true);
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// We found a corrupted entry. Stop here.
			// Delete the corrupted file.
			// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
			Logger.LogError("Immature index got corrupted. Deleting it...");
			ImmatureIndexFileManager.DeleteMe();
			throw;
		}
	}

	/// <remarks>Requires <see cref="ImmatureIndexAsyncLock"/> lock acquired.</remarks>
	private void ProcessLineNoLock(string line, bool enqueue)
	{
		var filter = FilterModel.FromLine(line);
		if (!TryProcessFilterNoLock(filter, enqueue))
		{
			throw new InvalidOperationException("Index file inconsistency detected.");
		}
	}

	/// <remarks>Requires <see cref="ImmatureIndexAsyncLock"/> lock acquired.</remarks>
	private bool TryProcessFilterNoLock(FilterModel filter, bool enqueue)
	{
		try
		{
			SmartHeaderChain.AppendTip(filter.Header);

			if (enqueue)
			{
				ImmatureFilters.Add(filter);
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
	}

	public async Task AddNewFiltersAsync(IEnumerable<FilterModel> filters)
	{
		var successAny = false;

		foreach (var filter in filters)
		{
			var success = false;

			using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			{
				success = TryProcessFilterNoLock(filter, enqueue: true);
			}

			successAny = successAny || success;

			if (success)
			{
				NewFilter?.Invoke(this, filter); // Event always outside the lock.
			}
		}

		if (successAny)
		{
			AbandonedTasks.AddAndClearCompleted(TryCommitToFileAsync(TimeSpan.FromSeconds(3)));
		}
	}

	public async Task<FilterModel> RemoveLastFilterAsync()
	{
		FilterModel? filter = null;

		using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			filter = ImmatureFilters.Last();
			ImmatureFilters.RemoveLast();
			if (SmartHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(ImmatureFilters)} are not in sync.");
			}
			SmartHeaderChain.RemoveTip();
		}

		Reorged?.Invoke(this, filter);

		AbandonedTasks.AddAndClearCompleted(TryCommitToFileAsync(TimeSpan.FromSeconds(3)));

		return filter;
	}

	public async Task<IEnumerable<FilterModel>> RemoveAllImmatureFiltersAsync()
	{
		var removed = new List<FilterModel>();
		using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			if (ImmatureFilters.Any())
			{
				Logger.LogWarning($"Filters got corrupted. Reorging {ImmatureFilters.Count} immature filters in an attempt to fix them.");
			}
			else
			{
				Logger.LogCritical("Filters got corrupted and have no more immature filters. Deleting all filters and crashing the software...");

				using (await MatureIndexAsyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
				using (await ImmatureIndexAsyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
				{
					ImmatureIndexFileManager.DeleteMe();
					MatureIndexFileManager.DeleteMe();
				}

				Environment.Exit(2);
			}
		}

		while (ImmatureFilters.Any())
		{
			removed.Add(await RemoveLastFilterAsync().ConfigureAwait(false));
		}

		return removed;
	}

	/// <summary>
	/// It'll LogError the exceptions.
	/// If cancelled, it'll LogTrace the exception.
	/// </summary>
	private async Task TryCommitToFileAsync(TimeSpan throttle)
	{
		try
		{
			// If throttle is requested, then throttle.
			if (throttle != TimeSpan.Zero)
			{
				// Increment the throttle ID and remember the incremented value.
				int incremented = Interlocked.Increment(ref _throttleId);

				if (incremented < 21)
				{
					await Task.Delay(throttle, CancellationToken.None).ConfigureAwait(false);
				}

				// If the _throttleId is still the incremented value, then I am the latest CommitToFileAsync request.
				//	In this case I want to make the _throttledId 0 and go ahead and do the writeline.
				// If the _throttledId is not the incremented value anymore then I am not the latest request here,
				//	So just return, the latest request will do the file write in its own time.
				if (Interlocked.CompareExchange(ref _throttleId, 0, incremented) != incremented)
				{
					return;
				}
			}
			else
			{
				Interlocked.Exchange(ref _throttleId, 0); // So to notify the currently throttled threads that they do not have to run.
			}

			using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			using (await MatureIndexAsyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			using (await ImmatureIndexAsyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			{
				// Do not feed the cancellationToken here I always want this to finish running for safety.
				var currentImmatureLines = ImmatureFilters.Select(x => x.ToLine()).ToArray(); // So we do not read on ImmatureFilters while removing them.
				var matureLinesToAppend = currentImmatureLines.SkipLast(100);
				var immatureLines = currentImmatureLines.TakeLast(100);

				// The order of the following lines is important.

				// 1) First delete the immature index. If we lose it because the mature index writing fails, we are OK with that.
				ImmatureIndexFileManager.DeleteMe();

				// 2) Attempt to update the mature index.
				await MatureIndexFileManager.AppendAllLinesAsync(matureLinesToAppend, CancellationToken.None).ConfigureAwait(false);

				// 3) Create new immature index.
				await ImmatureIndexFileManager.WriteAllLinesAsync(immatureLines, CancellationToken.None).ConfigureAwait(false);

				while (ImmatureFilters.Count > 100)
				{
					ImmatureFilters.RemoveFirst();
				}
			}
		}
		catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
		{
			Logger.LogTrace(ex);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public async Task ForeachFiltersAsync(Func<FilterModel, Task> todo, Height fromHeight, CancellationToken cancellationToken)
	{
		using (await IndexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		using (await MatureIndexAsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var firstImmatureHeight = ImmatureFilters.FirstOrDefault()?.Header?.Height;
			if (!firstImmatureHeight.HasValue || firstImmatureHeight.Value > fromHeight)
			{
				if (MatureIndexFileManager.Exists())
				{
					uint height = StartingHeight;
					using var sr = MatureIndexFileManager.OpenText();

					while (true)
					{
						if (firstImmatureHeight == height)
						{
							break; // Let's use our the immature filters from here on. The content is the same, just someone else modified the file.
						}

						string? line = await sr.ReadLineAsync(CancellationToken.None).ConfigureAwait(false);

						if (line is null)
						{
							break;
						}

						if (height < fromHeight.Value)
						{
							height++;
							continue;
						}

						FilterModel filter = FilterModel.FromLine(line);

						await todo(filter).ConfigureAwait(false);
						height++;
					}
				}
			}

			foreach (FilterModel filter in ImmatureFilters.ToImmutableArray())
			{
				await todo(filter).ConfigureAwait(false);
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		await AbandonedTasks.WhenAllAsync().ConfigureAwait(false);
	}
}
