using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using NBitcoin.RPC;
using Nito.AsyncEx;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.BlockFilters;

// Parameters for controlling the service behavior
public record IndexBuilderServiceOptions(
	TimeSpan DelayForNodeToCatchUp,
	TimeSpan DelayAfterEverythingIsDone,
	TimeSpan DelayInCaseOfError);

public class IndexBuilderService : BackgroundService
{
	// Script used for dummy filters
	public static readonly byte[][] DummyScript = new byte[][] { ByteHelpers.FromHex("0009BBE4C2D17185643765C265819BF5261755247D") };

	// Dependencies
	private readonly IRPCClient _rpcClient;
	private readonly string _indexFilePath;
	private readonly BlockFilterSqliteStorage _indexStorage;
	private readonly AsyncLock _indexLock = new();
	private readonly uint _startingHeight;
	private readonly IndexBuilderServiceOptions _options;

	public IndexBuilderService(IRPCClient rpc, string indexFilePath, IndexBuilderServiceOptions? options = null)
	{
		_options = options ?? new IndexBuilderServiceOptions(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));
		_rpcClient = Guard.NotNull(nameof(rpc), rpc);

		_indexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);
		_startingHeight = SmartHeader.GetStartingHeader(_rpcClient.Network).Height;


		IoHelpers.EnsureContainingDirectoryExists(_indexFilePath);

		if (_rpcClient.Network == Network.RegTest && File.Exists(_indexFilePath))
		{
			File.Delete(_indexFilePath); // RegTest is not a global ledger, better to delete it.
		}

		_indexStorage = CreateBlockFilterSqliteStorage();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				FilterModel? lastFilter;
				using (await _indexLock.LockAsync(stoppingToken).ConfigureAwait(false))
				{
					lastFilter = GetLastFilter();
				}
				var (currentHeight, currentHash) = lastFilter is not null
					? (lastFilter.Header.Height, lastFilter.Header.BlockHash)
					: (_startingHeight - 1, uint256.Zero);

				var blockchainInfo = await _rpcClient.GetBlockchainInfoAsync(stoppingToken).ConfigureAwait(false);

				// If wasabi filter height is the same as core we may be done.
				if (blockchainInfo.Blocks-1 == currentHeight)
				{
					var timeToWait = blockchainInfo.IsSynchronized() && !blockchainInfo.InitialBlockDownload
						? _options.DelayAfterEverythingIsDone // Check that core is fully synced
						: _options.DelayForNodeToCatchUp; // Bitcoin Node is catching up give some time
					await Task.Delay(timeToWait, stoppingToken).ConfigureAwait(false);
					continue;
				}

				var nextHeight = currentHeight + 1;
				var blockHash = await _rpcClient.GetBlockHashAsync((int)nextHeight, stoppingToken).ConfigureAwait(false);
				var block = await _rpcClient.GetVerboseBlockAsync(blockHash, stoppingToken ).ConfigureAwait(false);

				// Check if we are still on the best chain,
				// if not rewind filters till we find the fork.
				if (currentHash != block.PrevBlockHash)
				{
					Logger.LogWarning($"Reorg observed on the network. Expected prev hash {currentHash} but got {block.PrevBlockHash}");

					await ReorgOneAsync(stoppingToken).ConfigureAwait(false);

					// Skip the current block.
					continue;
				}

				var filter = BuildFilterForBlock(block);

				var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, nextHeight, block.BlockTime);
				var filterModel = new FilterModel(smartHeader, filter);

				using (await _indexLock.LockAsync(stoppingToken).ConfigureAwait(false))
				{
					_indexStorage.TryAppend(filterModel);
				}

				// If not close to the tip, just log debug.
				var logLevel = blockchainInfo.Blocks - nextHeight <= 3 || nextHeight % 100 == 0 ? LogLevel.Info : LogLevel.Debug;
				Logger.Log(logLevel, $"Created filter for block: {nextHeight}  {blockHash}.");
			}
			catch (OperationCanceledException) // Do not log because it was requested by the user
			{
				throw;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);

				// Pause the while loop for a while to not flood logs in case of permanent error.
				await Task.Delay(_options.DelayInCaseOfError, stoppingToken).ConfigureAwait(false);
			}
		}
	}

	internal static GolombRiceFilter BuildFilterForBlock(VerboseBlockInfo block)
	{
		var scripts = FetchScripts(block);

		var entries = scripts.Count == 0 ? DummyScript : scripts.Select(x => x.ToCompressedBytes());
		return new GolombRiceFilterBuilder()
			.SetKey(block.Hash)
			.SetP(20)
			.SetM(1 << 20)
			.AddEntries(entries)
			.Build();
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

	private async Task ReorgOneAsync(CancellationToken cancellationToken)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			if(_indexStorage.TryRemoveLast(out var removedFilter))
			{
				Logger.LogInfo($"REORG invalid block: {removedFilter.Header.BlockHash}");
			}
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

	public async Task<(Height bestHeight, IEnumerable<FilterModel> filters, bool found)> GetFilterLinesExcludingAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancellationToken = default)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var filterModels = _indexStorage.FetchNewerThanBlockHash(bestKnownBlockHash, count).ToList();
			if (filterModels.Count > 0)
			{
				return (new Height((uint)_indexStorage.GetBestHeight()), filterModels, true);
			}

			var lastFilter = GetLastFilter();
			return  lastFilter is null
				? (new Height(HeightType.Unknown), [], false)
				: (new Height(lastFilter.Header.Height), [], lastFilter.Header.BlockHash == bestKnownBlockHash);
		}
	}

	public FilterModel? GetLastFilter()
	{
		// Note: This method should be called with the lock already acquired (for tests it is okay)
		var lastFilterList = _indexStorage.FetchLast(1).ToList();
		return lastFilterList.Count == 0 ? null : lastFilterList[0];
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		// Then stop the background service
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}
}

public static class BlockchainInfoExtensions
{
	public static bool IsSynchronized(this BlockchainInfo me) =>
		me.Blocks == me.Headers;
}
