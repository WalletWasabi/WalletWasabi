using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Wallets.SilentPayment;

namespace WalletWasabi.Blockchain.BlockFilters;

public class IndexBuilderService
{
	private const long NotStarted = 0;
	private const long Running = 1;
	private const long Stopping = 2;
	private const long Stopped = 3;

	/// <summary>
	/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
	/// </summary>
	private long _serviceStatus;

	private long _workerCount;

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

		IndexStorage = CreateBlockFilterSqliteStorage();
		_blockNotifier.OnBlock += BlockNotifier_OnBlock;
	}

	public static byte[][] DummyScript { get; } = new byte[][] { ByteHelpers.FromHex("0009BBE4C2D17185643765C265819BF5261755247D") };

	private readonly IRPCClient _rpcClient;
	private readonly BlockNotifier _blockNotifier;
	private readonly string _indexFilePath;
	private BlockFilterSqliteStorage IndexStorage { get; set; }

	/// <remarks>Guards <see cref="Index"/>.</remarks>
	private readonly object _indexLock = new();
	private readonly uint _startingHeight;
	public bool IsRunning => Interlocked.Read(ref _serviceStatus) == Running;
	private bool IsStopping => Interlocked.Read(ref _serviceStatus) >= Stopping;
	public DateTimeOffset LastFilterBuildTime { get; set; }

	private readonly RpcPubkeyType[] _pubKeyTypes = [RpcPubkeyType.TxWitnessV0Keyhash, RpcPubkeyType.TxWitnessV1Taproot];

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
					await Task.Delay(100).ConfigureAwait(false);
				}

				if (IsStopping)
				{
					return;
				}

				try
				{
					Interlocked.Exchange(ref _serviceStatus, Running);

					while (IsRunning)
					{
						try
						{
							SyncInfo syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);

							FilterModel? lastIndexFilter;

							lock (_indexLock)
							{
								lastIndexFilter = GetLastFilter();
							}

							uint currentHeight;
							uint256? currentHash;

							if (lastIndexFilter is not null)
							{
								currentHeight = lastIndexFilter.Header.Height;
								currentHash = lastIndexFilter.Header.BlockHash;
							}
							else
							{
								currentHash = _startingHeight == 0
									? uint256.Zero
									: await _rpcClient.GetBlockHashAsync((int)_startingHeight - 1).ConfigureAwait(false);
								currentHeight = _startingHeight - 1;
							}

							var coreNotSynced = !syncInfo.IsCoreSynchronized;
							var tipReached = syncInfo.BlockCount == currentHeight;
							var isTimeToRefresh = DateTimeOffset.UtcNow - syncInfo.BlockchainInfoUpdated > TimeSpan.FromMinutes(5);
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
									// Knots is catching up give it a 10 seconds
									await Task.Delay(10000).ConfigureAwait(false);
									continue;
								}
							}

							uint nextHeight = currentHeight + 1;
							uint256 blockHash = await _rpcClient.GetBlockHashAsync((int)nextHeight).ConfigureAwait(false);
							VerboseBlockInfo block = await _rpcClient.GetVerboseBlockAsync(blockHash).ConfigureAwait(false);

							// Check if we are still on the best chain,
							// if not rewind filters till we find the fork.
							if (currentHash != block.PrevBlockHash)
							{
								Logger.LogWarning("Reorg observed on the network.");

								ReorgOne();

								// Skip the current block.
								continue;
							}

							var filterTask = Task.Run(() => BuildFilterForBlock(block, _pubKeyTypes));
							var tweakDataTask = Task.Run(() => BuildSilentPaymentTweakData(block).ToArray());

							await Task.WhenAll(filterTask, tweakDataTask).ConfigureAwait(false);
							var filter = await filterTask.ConfigureAwait(false);
							var tweakData = await tweakDataTask.ConfigureAwait(false);

							var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, nextHeight, block.BlockTime);
							var filterModel = new FilterModel(smartHeader, filter, tweakData);

							lock (_indexLock)
							{
								IndexStorage.TryAppend(filterModel);
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
							LastFilterBuildTime = DateTimeOffset.UtcNow;
						}
						catch (Exception ex)
						{
							Logger.LogError(ex);

							// Pause the while loop for a while to not flood logs in case of permanent error.
							await Task.Delay(1000).ConfigureAwait(false);
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

	private IEnumerable<byte[]> BuildSilentPaymentTweakData(VerboseBlockInfo block)
	{
		foreach (var tx in block.Transactions)
		{
			var inputs = tx.Inputs.OfType<VerboseInputInfo.Full>().ToList();
			if (inputs.Count < tx.Inputs.Count())
			{
				continue;
			}

			var hasAtLeastOneTaprootOutput = tx.Outputs.Any(x => x.ScriptPubKey.IsScriptType(ScriptType.Taproot));
			var pubKeys = inputs
				.Select(i => SilentPayment.ExtractPubKey(i.ScriptSig, i.WitScript, i.PrevOut.ScriptPubKey))
				.DropNulls()
				.ToArray();

			if (hasAtLeastOneTaprootOutput && pubKeys.Length > 0)
			{
				var prevOuts = inputs.Select(x => x.OutPoint).ToArray();
				yield return SilentPayment.TweakData(prevOuts, pubKeys).ToBytes();
			}
		}
	}

	internal static GolombRiceFilter BuildFilterForBlock(VerboseBlockInfo block, RpcPubkeyType[] pubKeyTypes)
	{
		var scripts = FetchScripts(block, pubKeyTypes);

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

	private static List<Script> FetchScripts(VerboseBlockInfo block, RpcPubkeyType[] pubKeyTypes)
	{
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
			if(IndexStorage.TryRemoveLast(out var removedFilter))
			{
				Logger.LogInfo($"REORG invalid block: {removedFilter.Header.BlockHash}");
			}
		}
	}

	private async Task<SyncInfo> GetSyncInfoAsync()
	{
		var bcinfo = await _rpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
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

	public (Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(uint256 bestKnownBlockHash, int count, out bool found)
	{
		lock (_indexLock)
		{
			var filterModels = IndexStorage.FetchNewerThanBlockHash(bestKnownBlockHash, count).ToList();
			uint bestHeight;
			if (filterModels.Count > 0)
			{
				bestHeight = (uint)IndexStorage.GetBestHeight();
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
			var lastFilterList = IndexStorage.FetchLast(1).ToList();
			return lastFilterList.Count == 0 ? null : lastFilterList[0];
		}
	}

	public async Task StopAsync()
	{
		if (_blockNotifier is { })
		{
			_blockNotifier.OnBlock -= BlockNotifier_OnBlock;
		}

		Interlocked.CompareExchange(ref _serviceStatus, Stopping, Running); // If running, make it stopping.

		while (Interlocked.CompareExchange(ref _serviceStatus, Stopped, NotStarted) == 2)
		{
			await Task.Delay(50).ConfigureAwait(false);
		}
	}
}
