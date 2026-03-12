using Microsoft.Data.Sqlite;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class FilterStore : IFilterStore, IAsyncDisposable
{
	public FilterStore(string workFolderPath, Network network, SmartHeaderChain smartHeaderChain)
	{
		_network = network;
		_smartHeaderChain = smartHeaderChain;

		workFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(workFolderPath);

		_storageFilePath = Path.Combine(workFolderPath, "IndexStore.sqlite");

		if (network == Network.RegTest)
		{
			DeleteIndex(_storageFilePath);
		}

		IndexStorage = CreateBlockFilterSqliteStorage();
	}

	private BlockFilterSqliteStorage CreateBlockFilterSqliteStorage()
	{
		try
		{
			return BlockFilterSqliteStorage.FromFile(dataSource: _storageFilePath);
		}
		catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 11) // 11 ~ SQLITE_CORRUPT error code
		{
			Logger.LogError($"Failed to open SQLite storage file because it's corrupted. Deleting the storage file '{_storageFilePath}'.");

			DeleteIndex(_storageFilePath);
			throw;
		}
	}

	public event EventHandler<FilterModel>? Reorged;

	public event EventHandler<FilterModel[]>? NewFilters;

	/// <summary>SQLite file path for migration purposes.</summary>
	private readonly string _storageFilePath;

	/// <summary>NBitcoin network.</summary>
	private readonly Network _network;

	private readonly SmartHeaderChain _smartHeaderChain;

	/// <summary>Task completion source that is completed once a <see cref="InitializeAsync(CancellationToken)"/> finishes.</summary>
	/// <remarks><c>true</c> if it finishes successfully, <c>false</c> in all other cases.</remarks>
	public TaskCompletionSource<bool> InitializedTcs { get; } = new();

	/// <summary>Filter disk storage.</summary>
	/// <remarks>Guarded by <see cref="_indexLock"/>.</remarks>
	private BlockFilterSqliteStorage IndexStorage { get; set; }

	/// <summary>Guards <see cref="IndexStorage"/>.</summary>
	private readonly AsyncLock _indexLock = new();

	public async Task InitializeAsync(ChainHeight oldestKnownTransactionHeight, CancellationToken cancellationToken)
	{
		try
		{
			using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				var checkpoint = FilterCheckpoints.GetCheckpointForBirthday(oldestKnownTransactionHeight, _network);

				if (IndexStorage.GetPragmaUserVersion() < 2)
				{
					Logger.LogInfo("Migrating from old Indexer filters to Bitcoin Core RPC filters.");
					IndexStorage.Clear();
					IndexStorage.TryAppend(checkpoint);
					IndexStorage.SetPragmaUserVersion(2);
				}
				else
				{
					var currentMinHeight = IndexStorage.GetMinimumBlockHeight();

					if (currentMinHeight is { } minHeight && checkpoint.Header.Height < minHeight - 100)
					{
						Logger.LogInfo($"Recheckpointing filters: wallet with earlier birthday detected. " +
						               $"Current minimum height: {currentMinHeight.Value}, " +
						               $"Desired checkpoint: {checkpoint.Header.Height}");

						IndexStorage.Clear();
						IndexStorage.TryAppend(checkpoint);
					}
				}

				await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);

				// Initialization succeeded.
				InitializedTcs.SetResult(true);
			}
		}
		catch (Exception)
		{
			InitializedTcs.SetResult(false);
			throw;
		}
	}

	/// <remarks>Guarded by <see cref="_indexLock"/>.</remarks>
	private Task InitializeFiltersNoLockAsync(CancellationToken cancellationToken)
	{
		try
		{
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
			IndexStorage.SetPragmaUserVersion(0); // forces to recreate
			throw;
		}

		return Task.CompletedTask;
	}

	/// <remarks>Requires <see cref="_indexLock"/> lock acquired.</remarks>
	private bool TryProcessFilterNoLock(FilterModel filter, bool enqueue)
	{
		try
		{
			if (!IsCorrect(_smartHeaderChain, filter))
			{
				throw new InvalidOperationException($"Header doesn't point to previous header.");
			}

			_smartHeaderChain.AppendTip(filter.Header);

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

	private bool IsCorrect(SmartHeaderChain c, FilterModel m)
	{
		// If this is the first filter that we receive then it is correct only if it is starting one.
		if (c.Tip is null)
		{
			return true;
		}

		// We received a bip158-compatible filter, and it matches the tip's header, which means the previous filter
		// was also a bip158-compatible one, and it is the correct one.
		if (m.Filter.GetHeader(c.Tip.BlockFilterHeader) == m.Header.BlockFilterHeader)
		{
			return true;
		}

		return false;
	}

	public async Task AddNewFiltersAsync(IEnumerable<FilterModel> filters)
	{
		using (await _indexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			using SqliteTransaction sqliteTransaction = IndexStorage.BeginTransaction();

			int processed = 0;

			try
			{
				foreach (FilterModel filter in filters)
				{
					if (!TryProcessFilterNoLock(filter, enqueue: true))
					{
						throw new InvalidOperationException($"Failed to process filter with height {filter.Header.Height}.");
					}

					processed++;
				}
			}
			finally
			{
				sqliteTransaction.Commit();

				if (processed > 0)
				{
					NewFilters.SafeInvoke(this, filters.Take(processed).ToArray());
				}
			}
		}
	}

	public async Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return IndexStorage.Fetch(fromHeight: fromHeight, limit: batchSize).ToArray();
		}
	}

	public Task<FilterModel?> TryRemoveLastFilterAsync()
	{
		return TryRemoveLastFilterIfNewerThanAsync(height: null);
	}

	private async Task<FilterModel?> TryRemoveLastFilterIfNewerThanAsync(uint? height)
	{
		FilterModel? filter;

		using (await _indexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			if (height is null)
			{
				if (!IndexStorage.TryRemoveLast(out filter))
				{
					return null;
				}
			}
			else
			{
				if (!IndexStorage.TryRemoveLastIfNewerThan(height.Value, out filter))
				{
					return null;
				}
			}

			if (_smartHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(IndexStorage)} are not in sync.");
			}

			_smartHeaderChain.RemoveTip();
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

	private void DeleteIndex(string indexPath)
	{
		lock (_indexLock)
		{
			if (File.Exists(indexPath))
			{
				File.Delete(indexPath);
			}

			if (File.Exists($"{indexPath}-shm"))
			{
				File.Delete($"{indexPath}-shm");
			}

			if (File.Exists($"{indexPath}-wal"))
			{
				File.Delete($"{indexPath}-wal");
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
