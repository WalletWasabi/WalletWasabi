using Microsoft.Data.Sqlite;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Diagnostics;
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

		// Migration data.
		OldIndexFilePath = Path.Combine(workFolderPath, "MatureIndex.dat");
		OldImmatureIndexFilePath = Path.Combine(workFolderPath, "ImmatureIndex.dat");
		NewIndexFilePath = Path.Combine(workFolderPath, "IndexStore.sqlite");
		RunMigration = File.Exists(OldIndexFilePath) && !File.Exists(NewIndexFilePath);

		if (network == Network.RegTest)
		{
			File.Delete(NewIndexFilePath);
		}

		try
		{
			IndexStorage = BlockFilterSqliteStorage.FromFile(dataSource: NewIndexFilePath, startingFilter: StartingFilters.GetStartingFilter(network));
		}
		catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 11) // 11 ~ SQLITE_CORRUPT error code
		{
			Logger.LogError($"Failed to open SQLite storage file because it's corrupted. Deleting the storage file '{NewIndexFilePath}'.");

			// The database file can still be in use, clear all pools to unlock the filter database file.
			SqliteConnection.ClearAllPools();
			File.Delete(NewIndexFilePath);
			throw;
		}
	}

	public event EventHandler<FilterModel>? Reorged;

	public event EventHandler<FilterModel>? NewFilter;

	/// <summary>Mature index path for migration purposes.</summary>
	private string OldIndexFilePath { get; }

	/// <summary>Immature index path for migration purposes.</summary>
	private string OldImmatureIndexFilePath { get; }

	/// <summary>SQLite file path for migration purposes.</summary>
	private string NewIndexFilePath { get; }

	/// <summary>Run migration if SQLite file does not exist.</summary>
	private bool RunMigration { get; }

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

			// Migration code.
			if (RunMigration)
			{
				await Task.Run(MigrateToSqliteNoLock, cancellationToken).ConfigureAwait(false);
			}

			// If the automatic migration to SQLite is stopped or somehow disrupted, we would not delete the old index data.
			// So check it every time.
			RemoveOldIndexFilesIfExist();

			await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private void RemoveOldIndexFilesIfExist()
	{
		if (File.Exists(OldIndexFilePath))
		{
			try
			{
				File.Delete($"{OldImmatureIndexFilePath}.dig"); // No exception is thrown if file does not exist.
				File.Delete(OldImmatureIndexFilePath);
				File.Delete($"{OldIndexFilePath}.dig");
				File.Delete(OldIndexFilePath);

				Logger.LogInfo("Removed old index file data.");
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
		}
	}

	private void MigrateToSqliteNoLock()
	{
		int i = 0;

		try
		{
			Logger.LogWarning("Migration of block filters to SQLite format is about to begin. Please wait a moment.");

			Stopwatch stopwatch = Stopwatch.StartNew();

			List<string> filters = new(capacity: 100_000);

			using (FileStream fs = File.Open(OldIndexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (BufferedStream bs = new(fs))
			using (StreamReader sr = new(bs))
			{
				while (true)
				{
					i++;
					string? line = sr.ReadLine();

					if (line is null)
					{
						break;
					}

					// Starting filter is already added at this point.
					if (i == 1)
					{
						continue;
					}

					filters.Add(line);

					if (i % 100_000 == 0)
					{
						IndexStorage.BulkAppend(filters);
						filters.Clear();
					}
				}
			}

			IndexStorage.BulkAppend(filters);

			Logger.LogInfo($"Migration of {i} filters to SQLite was finished in {stopwatch.Elapsed} seconds.");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);

			SqliteConnection.ClearAllPools();

			// Do not run migration code again if it fails.
			File.Delete(NewIndexFilePath);
			File.Delete(OldIndexFilePath);
		}
	}

	/// <remarks>Guarded by <see cref="IndexLock"/>.</remarks>
	private Task InitializeFiltersNoLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			using IDisposable _ = BenchmarkLogger.Measure(LogLevel.Debug, "Block filters loading");

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
				if (!TryProcessFilterNoLock(filter, enqueue: true))
				{
					throw new InvalidOperationException($"Failed to process filter with height {filter.Header.Height}.");
				}
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
