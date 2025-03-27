using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.BlockFilters;

public class IndexBuilderService
{
    // Service status constants
	private const long NotStarted = 0;
	private const long Running = 1;
	private const long Stopping = 2;
	private const long Stopped = 3;

	// Script used for dummy filters
	public static readonly byte[][] DummyScript = new byte[][] { ByteHelpers.FromHex("0009BBE4C2D17185643765C265819BF5261755247D") };

	// Service state fields
	private long _serviceStatus;
	private long _workerCount;
	private readonly CancellationTokenSource _cts = new();
	private readonly IRPCClient _rpcClient;
	private readonly BlockNotifier _blockNotifier;
	private readonly string _indexFilePath;
	private readonly BlockFilterSqliteStorage _indexStorage;
	private readonly object _indexLock = new();
	private readonly uint _startingHeight;

	private readonly TimeSpan _syncRetryDelay = TimeSpan.FromSeconds(10);
	private readonly TimeSpan _blockchainInfoRefreshInterval = TimeSpan.FromMinutes(5);

	public IndexBuilderService(IRPCClient rpc, BlockNotifier blockNotifier, string indexFilePath)
	{
		_rpcClient = Guard.NotNull(nameof(rpc), rpc);
		_blockNotifier = Guard.NotNull(nameof(blockNotifier), blockNotifier);

		_indexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);
		_startingHeight = SmartHeader.GetStartingHeader(_rpcClient.Network).Height;

		_serviceStatus = NotStarted;

		IoHelpers.EnsureContainingDirectoryExists(_indexFilePath);

		if (_rpcClient.Network == Network.RegTest && File.Exists(_indexFilePath))
		{
			File.Delete(_indexFilePath); // RegTest is not a global ledger, better to delete it.
		}

		_indexStorage = CreateBlockFilterSqliteStorage();
		_blockNotifier.OnBlock += BlockNotifier_OnBlock;
	}


	public bool IsRunning => Interlocked.Read(ref _serviceStatus) == Running;
	private bool IsStopping => Interlocked.Read(ref _serviceStatus) >= Stopping;

	public void Synchronize()
	{
		Task.Run(async () =>
		{
			try
			{
				if (Interlocked.Read(ref _workerCount) >= 2)
				{
					return;
				}

				Interlocked.Increment(ref _workerCount);
				while (Interlocked.Read(ref _workerCount) != 1)
				{
					await Task.Delay(100, _cts.Token).ConfigureAwait(false);
				}

				if (IsStopping)
				{
					return;
				}

				try
				{
					Interlocked.Exchange(ref _serviceStatus, Running);

					while (IsRunning && !_cts.IsCancellationRequested)
					{
						try
						{
							var (currentHeight, currentHash) = GetLastFilter()?.Header is {BlockHash: var curBlockHash, Height: var curHeight}
								? (curHeight, curBlockHash)
								: (_startingHeight - 1, uint256.Zero);

							SyncInfo syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);
							var coreNotSynced = !syncInfo.IsCoreSynchronized;
							var tipReached = syncInfo.BlockCount == currentHeight;
							var isTimeToRefresh = DateTimeOffset.UtcNow - syncInfo.BlockchainInfoUpdated > _blockchainInfoRefreshInterval;
							if (coreNotSynced || tipReached || isTimeToRefresh)
							{
								syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);
							}

							// If wasabi filter height is the same as core we may be done.
							if (syncInfo.BlockCount == currentHeight)
							{
								// Check that core is fully synced
								if (syncInfo.IsCoreSynchronized && !syncInfo.InitialBlockDownload)
								{
									// Mark the process as not-started, so it can be started again
									// and finally block can mark it as stopped.
									Interlocked.Exchange(ref _serviceStatus, NotStarted);
									return;
								}
								else
								{
									// Bitcoin Node is catching up give it a 10 seconds
									await Task.Delay(_syncRetryDelay, _cts.Token).ConfigureAwait(false);
									continue;
								}
							}

							uint nextHeight = currentHeight + 1;
							uint256 blockHash = await _rpcClient.GetBlockHashAsync((int)nextHeight, _cts.Token).ConfigureAwait(false);
							VerboseBlockInfo block = await _rpcClient.GetVerboseBlockAsync(blockHash, _cts.Token).ConfigureAwait(false);

							// Check if we are still on the best chain,
							// if not rewind filters till we find the fork.
							if (currentHash != block.PrevBlockHash)
							{
								Logger.LogWarning("Reorg observed on the network.");

								ReorgOne();

								// Skip the current block.
								continue;
							}

							var filter = BuildFilterForBlock(block);

							var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, nextHeight, block.BlockTime);
							var filterModel = new FilterModel(smartHeader, filter);

							lock (_indexLock)
							{
								_indexStorage.TryAppend(filterModel);
							}

							// If not close to the tip, just log debug.
							if (syncInfo.BlockCount - nextHeight <= 3 || nextHeight % 100 == 0)
							{
								Logger.LogInfo($"Created filter for block: {nextHeight}.");
							}
							else
							{
								Logger.LogDebug($"Created filter for block: {nextHeight}.");
							}
						}
						catch (OperationCanceledException) // Do not log because it was requested by the user
						{
							throw;
						}
						catch (Exception ex)
						{
							Logger.LogError(ex);

							// Pause the while loop for a while to not flood logs in case of permanent error.
							await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _serviceStatus, Stopped, Stopping); // If IsStopping, make it stopped.
					Interlocked.Decrement(ref _workerCount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Synchronization attempt failed to start: {ex}");
			}
		});
	}

	internal static GolombRiceFilter BuildFilterForBlock(VerboseBlockInfo block)
	{
		var scripts = FetchScripts(block);

		if (scripts.Count != 0)
		{
			return new GolombRiceFilterBuilder()
				.SetKey(block.Hash)
				.SetP(20)
				.SetM(1 << 20)
				.AddEntries(scripts.Select(x => x.ToCompressedBytes()))
				.Build();
		}
		else
		{
			// We cannot have empty filters, because there was a bug in GolombRiceFilterBuilder that evaluates empty filters to true.
			// And this must be fixed in a backwards compatible way, so we create a fake filter with a random scp instead.
			return CreateDummyEmptyFilter(block.Hash);
		}
	}

	private static List<Script> FetchScripts(VerboseBlockInfo block)
	{
		var pubKeyTypes = new[] { RpcPubkeyType.TxWitnessV0Keyhash, RpcPubkeyType.TxWitnessV1Taproot };
		var scripts = new List<Script>();

		foreach (var tx in block.Transactions)
		{
			foreach (var input in tx.Inputs)
			{
				switch (input)
				{
					case VerboseInputInfo.Coinbase:
						break;
					case VerboseInputInfo.Full inputInfo:
						if (pubKeyTypes.Contains(inputInfo.PrevOut.PubkeyType))
						{
							scripts.Add(inputInfo.PrevOut.ScriptPubKey);
						}
						break;
					case VerboseInputInfo.None inputInfo:
						// This happens when the block containing the prevOut can't be found (pruned)
						// If the block is previous to segwit activation then everything is okay because the scriptPubKey
						// is not segwit or taproot. However, if the block is after segwit activation, that means that
						// the scriptPubKey could be segwit or taproot and the filter can be incomplete/broken.
						throw new InvalidOperationException($"{inputInfo.Outpoint} script information is not available.");
				}
			}

			foreach (var output in tx.Outputs)
			{
				if (pubKeyTypes.Contains(output.PubkeyType))
				{
					scripts.Add(output.ScriptPubKey);
				}
			}
		}

		return scripts;
	}

	private void ReorgOne()
	{
		lock (_indexLock)
		{
			if(_indexStorage.TryRemoveLast(out var removedFilter))
			{
				Logger.LogInfo($"REORG invalid block: {removedFilter.Header.BlockHash}");
			}
		}
	}

	private async Task<SyncInfo> GetSyncInfoAsync()
	{
		var bcinfo = await _rpcClient.GetBlockchainInfoAsync(_cts.Token).ConfigureAwait(false);
		var pbcinfo = new SyncInfo(bcinfo);
		return pbcinfo;
	}

	private void BlockNotifier_OnBlock(object? sender, Block e)
	{
		try
		{
			// Run sync every time a block notification arrives. Synchronizer will stop when it finishes.
			Synchronize();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private BlockFilterSqliteStorage CreateBlockFilterSqliteStorage()
	{
		try
		{
			return BlockFilterSqliteStorage.FromFile(dataSource: _indexFilePath, startingFilter: StartingFilters.GetStartingFilter(_rpcClient.Network));
		}
		catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 11) // 11 ~ SQLITE_CORRUPT error code
		{
			Logger.LogError($"Failed to open SQLite storage file because it's corrupted. Deleting the storage file '{_indexFilePath}'.");

			File.Delete(_indexFilePath);
			throw;
		}
	}

	public static GolombRiceFilter CreateDummyEmptyFilter(uint256 blockHash)
	{
		return new GolombRiceFilterBuilder()
			.SetKey(blockHash)
			.SetP(20)
			.SetM(1 << 20)
			.AddEntries(DummyScript)
			.Build();
	}

	public (Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(uint256 bestKnownBlockHash, int count, out bool found)
	{
		lock (_indexLock)
		{
			var filterModels = _indexStorage.FetchNewerThanBlockHash(bestKnownBlockHash, count).ToList();
			uint bestHeight;
			if (filterModels.Count > 0)
			{
				bestHeight = (uint)_indexStorage.GetBestHeight();
				found = true;
			}
			else
			{
				var lastFilter = GetLastFilter();
				if (lastFilter is null)
				{
					found = false;
					return (new Height(HeightType.Unknown), []);
				}

				found = lastFilter.Header.BlockHash == bestKnownBlockHash;
				bestHeight = lastFilter.Header.Height;
			}
			return (new Height(bestHeight), filterModels);
		}
	}

	public FilterModel? GetLastFilter()
	{
		lock (_indexLock)
		{
			var lastFilterList = _indexStorage.FetchLast(1).ToList();
			return lastFilterList.Count == 0 ? null : lastFilterList[0];
		}
	}

	public async Task StopAsync()
	{
		if (_blockNotifier is { })
		{
			_blockNotifier.OnBlock -= BlockNotifier_OnBlock;
		}

		// Cancel ongoing operations
		if (!_cts.IsCancellationRequested)
		{
			await _cts.CancelAsync().ConfigureAwait(false);
		}

		Interlocked.CompareExchange(ref _serviceStatus, Stopping, Running); // If running, make it stopping.

		while (Interlocked.CompareExchange(ref _serviceStatus, Stopped, NotStarted) == 2)
		{
			await Task.Delay(50, _cts.Token).ConfigureAwait(false);
		}
	}
}
