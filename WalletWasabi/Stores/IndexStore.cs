using Microsoft.Data.Sqlite;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class IndexStore : IAsyncDisposable
{
	public IndexStore(string workFolderPath, Network network, SmartHeaderChain smartHeaderChain)
	{
		SmartHeaderChain = smartHeaderChain;

		workFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(workFolderPath);

		string indexFilePath = Path.Combine(workFolderPath, "IndexStore.sqlite");
		IndexStorage = BlockFilterSqliteStorage.FromFile(path: indexFilePath, startingFilter: StartingFilters.GetStartingFilter(network));

		if (network == Network.RegTest)
		{
			IndexStorage.Clear(); // RegTest is not a global ledger, better to delete it.
		}
	}

	public event EventHandler<FilterModel>? Reorged;
	public event EventHandler<FilterModel>? NewFilter;
	public SmartHeaderChain SmartHeaderChain { get; }

	/// <summary>Filter disk storage.</summary>
	/// <remarks>Guarded by <see cref="IndexLock"/>.</remarks>
	private BlockFilterSqliteStorage IndexStorage { get; }

	/// <summary>Guards <see cref="IndexStorage"/>.</summary>
	private AsyncLock IndexLock { get; } = new();

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		using IDisposable _ = BenchmarkLogger.Measure();

		using (await IndexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			cancellationToken.ThrowIfCancellationRequested();

			await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <remarks>Guarded by <see cref="IndexLock"/>.</remarks>
	private Task InitializeFiltersNoLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			// TODO: Replace the message with a new one. Staying with this one to allow comparisons.
			using IDisposable _ = BenchmarkLogger.Measure(LogLevel.Debug, "MatureIndexFileManager loading");

			int i = 0;

			// Read last N filters. There is no need to read all of them.
			foreach (FilterModel filter in IndexStorage.FetchLast(n: 5000))
			{
				i++;

				if (!TryProcessFilterNoLock(filter, enqueue: false))
				{
					throw new InvalidOperationException("Index file inconsistency detected.");
				}

				cancellationToken.ThrowIfCancellationRequested();
			}

			Logger.LogDebug($"Loaded {i} lines from the mature index file.");
		}
		catch (InvalidOperationException ex)
		{
			// We found a corrupted entry. Clear the corrupted database and stop here.
			Logger.LogError("Filter index got corrupted. Clearing the filter index...");
			Logger.LogDebug(ex);
			IndexStorage.Clear();
			throw;
		}

		return Task.CompletedTask;
	}

	/// <remarks>Requires <see cref="IndexLock"/> lock acquired.</remarks>
	private bool TryProcessFilterNoLock(FilterModel filter, bool enqueue)
	{
		try
		{
			SmartHeaderChain.AppendTip(filter.Header);

			if (enqueue)
			{
				if (!IndexStorage.TryAppend(filter))
				{
					throw new InvalidOperationException("Failed to append filter to the database.");
				}
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
		if (NewFilter is null)
		{
			// Lock once.
			using IDisposable lockDisposable = await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false);

			using SqliteTransaction sqliteTransaction = IndexStorage.BeginTransaction();

			foreach (FilterModel filter in filters)
			{
				// TODO: Should throw?
				_ = TryProcessFilterNoLock(filter, enqueue: true);
			}

			sqliteTransaction.Commit();
		}
		else
		{
			foreach (FilterModel filter in filters)
			{
				bool success;

				using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
				{
					success = TryProcessFilterNoLock(filter, enqueue: true);
				}

				if (success)
				{
					NewFilter?.Invoke(this, filter); // Event always outside the lock.
				}
			}
		}
	}

	public Task<FilterModel?> TryRemoveLastFilterAsync()
	{
		return TryRemoveLastFilterIfNewerThanAsync(height: null);
	}

	private async Task<FilterModel?> TryRemoveLastFilterIfNewerThanAsync(uint? height)
	{
		FilterModel? filter;

		using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			if (height is null)
			{
				if (!IndexStorage.TryRemoveLast(out filter))
				{
					throw new InvalidOperationException("No last filter.");
				}
			}
			else
			{
				if (!IndexStorage.TryRemoveLastIfNewerThan(height.Value, out filter))
				{
					throw new InvalidOperationException("No last filter.");
				}
			}

			if (SmartHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(IndexStorage)} are not in sync.");
			}

			SmartHeaderChain.RemoveTip();
		}

		Reorged?.Invoke(this, filter);

		return filter;
	}

	public async Task RemoveAllNewerThanAsync(uint height)
	{
		while (true)
		{
			FilterModel? filterModel = await TryRemoveLastFilterIfNewerThanAsync(height).ConfigureAwait(false);

			if (filterModel is null)
			{
				break;
			}
		}
	}

	public async Task ForeachFiltersAsync(Func<FilterModel, Task> todo, Height fromHeight, CancellationToken cancellationToken)
	{
		using (await IndexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (FilterModel filter in IndexStorage.Fetch(fromHeight: fromHeight.Value))
			{
				await todo(filter).ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		IndexStorage.Dispose();
		return ValueTask.CompletedTask;
	}
}
