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
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class FilterStore : IFilterStore, IDisposable
{
	public FilterStore(string workFolderPath, Network network, FilterHeaderChain filterHeaderChain, EventBus eventBus)
	{
		_network = network;
		_filterHeaderChain = filterHeaderChain;
		_eventBus = eventBus;
		_storageFilePath = Path.Combine(workFolderPath, "IndexStore.sqlite");

		IoHelpers.EnsureDirectoryExists(workFolderPath);

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
			var storage = BlockFilterSqliteStorage.FromFile(dataSource: _storageFilePath);
			if (storage.GetPragmaUserVersion() < 2)
			{
				storage.Dispose();
				Logger.LogInfo("Migrating from old Indexer filters to Bitcoin Core RPC filters.");
				DeleteIndex(_storageFilePath);
				storage = BlockFilterSqliteStorage.FromFile(dataSource: _storageFilePath);
				storage.SetPragmaUserVersion(2);
			}

			return storage;
		}
		catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 11) // 11 ~ SQLITE_CORRUPT error code
		{
			Logger.LogError($"Failed to open SQLite storage file because it's corrupted. Deleting the storage file '{_storageFilePath}'.");

			DeleteIndex(_storageFilePath);
			var storage = BlockFilterSqliteStorage.FromFile(dataSource: _storageFilePath);
			storage.SetPragmaUserVersion(2);
			return storage;
		}
	}

	/// <summary>SQLite file path for migration purposes.</summary>
	private readonly string _storageFilePath;

	/// <summary>NBitcoin network.</summary>
	private readonly Network _network;

	private readonly FilterHeaderChain _filterHeaderChain;
	private readonly EventBus _eventBus;

	/// <summary>Filter disk storage.</summary>
	/// <remarks>Guarded by <see cref="_indexLock"/>.</remarks>
	private BlockFilterSqliteStorage IndexStorage { get; set; }

	/// <summary>Guards <see cref="IndexStorage"/>.</summary>
	private readonly AsyncLock _indexLock = new();

	/// <summary>
	/// Returns the minimum block height stored in the filter index, or <c>null</c> if the index is empty.
	/// </summary>
	public uint? GetMinimumBlockHeight()
	{
		return IndexStorage.GetMinimumBlockHeight();
	}

	public FilterModel? GetTip()
	{
		return IndexStorage.FetchLast(1).LastOrDefault();
	}

	public async Task InitializeAsync(ChainHeight oldestKnownTransactionHeight, CancellationToken cancellationToken)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var checkpoint = FilterCheckpoints.GetCheckpointForBirthday(oldestKnownTransactionHeight, _network);

			if (GetTip() is null)
			{
				IndexStorage.TryAppend(checkpoint);
			}
			else
			{
				var currentMinHeight = IndexStorage.GetMinimumBlockHeight();

				if (currentMinHeight is { } minHeight && checkpoint.Header.Height < minHeight)
				{
					Logger.LogInfo($"Recheckpointing filters: wallet with earlier birthday detected. " +
								   $"Current minimum height: {currentMinHeight.Value}, " +
								   $"Desired checkpoint: {checkpoint.Header.Height}");

					IndexStorage.Clear();
					IndexStorage.TryAppend(checkpoint);
				}
			}

			await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);
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
			var appendResult = _filterHeaderChain.TryAppendTip(filter.Header);
			if (appendResult)
			{
				_eventBus.Publish(new ClientTipHeightChanged(filter.Header.Height));

				if (enqueue)
				{
					if (!IndexStorage.TryAppend(filter))
					{
						throw new InvalidOperationException("Failed to append filter to the database.");
					}
				}

				return true;
			}

			return false;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
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
					_eventBus.Publish(new FiltersReceived(filters.Take(processed).ToArray()));
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

			if (_filterHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(FilterHeaderChain)} and {nameof(IndexStorage)} are not in sync.");
			}

			_filterHeaderChain.RemoveTip();
			_eventBus.Publish(new ClientTipHeightChanged(_filterHeaderChain.TipHeight));
		}

		_eventBus.Publish(new ChainReorganized(filter));

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

	public void Dispose()
	{
		IndexStorage.Dispose();
	}
}
