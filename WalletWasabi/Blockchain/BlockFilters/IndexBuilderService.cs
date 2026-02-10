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

public delegate Task<Result<FilterModel, string>> BlockFilterGenerator(
	IRPCClient rpc, uint256 blockHash, uint blockHeight, uint256 expectedPrevHash, CancellationToken cancellationToken);

// Parameters for controlling the service behavior
public record IndexBuilderServiceOptions(
	TimeSpan DelayForNodeToCatchUp,
	TimeSpan DelayAfterEverythingIsDone,
	TimeSpan DelayInCaseOfError);

public class IndexBuilderService : BackgroundService
{
	private readonly BlockFilterGenerator _generatorBlockFilter;

	// Dependencies
	private readonly IRPCClient _rpcClient;
	private readonly string _indexFilePath;
	private readonly BlockFilterSqliteStorage _indexStorage;
	private readonly AsyncLock _indexLock = new();
	private readonly uint _startingHeight;
	private readonly IndexBuilderServiceOptions _options;

	private static readonly IndexBuilderServiceOptions DefaultIndexBuilderOptions =
		new (TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));

	public IndexBuilderService(IRPCClient rpc, string indexFilePath, BlockFilterGenerator? generatorBlockFilter = null, IndexBuilderServiceOptions? options = null)
	{
		_generatorBlockFilter = generatorBlockFilter ?? LegacyWasabiFilterGenerator.GenerateBlockFilterAsync;
		_options = options ?? DefaultIndexBuilderOptions;

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
		var lastFilter = await GetLastFilterAsync(stoppingToken).ConfigureAwait(false);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var (currentHeight, currentHash) = lastFilter is not null
					? (lastFilter.Header.Height, lastFilter.Header.BlockHash)
					: (_startingHeight - 1, uint256.Zero);

				var blockchainInfo = await _rpcClient.GetBlockchainInfoAsync(stoppingToken).ConfigureAwait(false);

				// If wasabi filter height is the same as core we may be done.
				if (blockchainInfo.Blocks == currentHeight)
				{
					var timeToWait = blockchainInfo.IsSynchronized() && !blockchainInfo.InitialBlockDownload
						? _options.DelayAfterEverythingIsDone // Check that core is fully synced
						: _options.DelayForNodeToCatchUp; // Bitcoin Node is catching up give some time
					await Task.Delay(timeToWait, stoppingToken).ConfigureAwait(false);
					continue;
				}

				var nextHeight = currentHeight + 1;
				var blockHash = await _rpcClient.GetBlockHashAsync((int)nextHeight, stoppingToken).ConfigureAwait(false);

				var blockFilterResult = await _generatorBlockFilter(_rpcClient, blockHash, nextHeight, currentHash, stoppingToken).ConfigureAwait(false);
				lastFilter = await blockFilterResult.Match(
					async filterModel =>
					{
						using (await _indexLock.LockAsync(stoppingToken).ConfigureAwait(false))
						{
							_indexStorage.TryAppend(filterModel);
						}

						// If not close to the tip, just log debug.
						var logLevel = blockchainInfo.Blocks - nextHeight <= 3 || nextHeight % 100 == 0
							? LogLevel.Info
							: LogLevel.Debug;
						Logger.Log(logLevel, $"Created filter for block: {nextHeight}  {blockHash}.");

						return filterModel;
					},
					async errorMessage =>
					{
						Logger.LogWarning(errorMessage);

						await ReorgOneAsync(stoppingToken).ConfigureAwait(false);
						lastFilter = await GetLastFilterAsync(stoppingToken).ConfigureAwait(false);
						return lastFilter ?? throw new InvalidOperationException("There is no blocks in available!");
					}).ConfigureAwait(false);
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

	private async Task ReorgOneAsync(CancellationToken cancellationToken)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			if(!_indexStorage.TryRemoveLast(out var removedFilter))
			{
				Logger.LogInfo("Failed to remove filter for REORG invalid block");
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

	public async Task<(ChainHeight bestHeight, IEnumerable<FilterModel> filters, bool found)> GetFilterLinesExcludingAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancellationToken = default)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var filterModels = _indexStorage.FetchNewerThanBlockHash(bestKnownBlockHash, count).ToList();
			if (filterModels.Count > 0)
			{
				return (new Height.ChainHeight((uint)_indexStorage.GetBestHeight()), filterModels, true);
			}

			var lastFilter = GetLastFilterNoLock();
			return  lastFilter is null
				? (ChainHeight.Genesis, [], false) // FIXME: This is an error return, we should use Result<S,E> here
				: (new ChainHeight(lastFilter.Header.Height), [], lastFilter.Header.BlockHash == bestKnownBlockHash);
		}
	}

	public async Task<FilterModel?> GetLastFilterAsync(CancellationToken cancellationToken)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return GetLastFilterNoLock();
		}
	}

	private FilterModel? GetLastFilterNoLock()
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

public static class BitcoinRpcBip158FilterFetcher
{
	public static async Task<Result<FilterModel, string>> FetchBlockFilterAsync(IRPCClient rpcClient, uint256 blockHash, uint blockHeight, uint256 expectedHeader, CancellationToken cancellationToken)
	{
		var blockFilter = await rpcClient.GetBlockFilterAsync(blockHash, cancellationToken).ConfigureAwait(false);

		if (expectedHeader != uint256.Zero && blockFilter.Filter.GetHeader(expectedHeader) != blockFilter.Header)
		{
			return Result<FilterModel, string>.Fail($"Reorg invalid block hash {blockHash} at {blockHeight}.");
		}
		var smartHeader = new SmartHeader(blockHash, blockFilter.Header, blockHeight, DateTimeOffset.MinValue);
		var filterModel = new FilterModel(smartHeader, blockFilter.Filter);
		return filterModel;
	}
}

public static class LegacyWasabiFilterGenerator
{
	public static async Task<Result<FilterModel,string>> GenerateBlockFilterAsync(IRPCClient rpcClient, uint256 blockHash, uint blockHeight, uint256 expectedPrevHash, CancellationToken cancellationToken)
	{
		var block = await rpcClient.GetVerboseBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
		if (expectedPrevHash != block.PrevBlockHash)
		{
			return Result<FilterModel, string>.Fail($"Reorg invalid block hash {blockHash} but got {block.PrevBlockHash} at {blockHeight}.");
		}

		var filter = BuildFilterForBlock(block);
		var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, blockHeight, block.BlockTime);
		var filterModel = new FilterModel(smartHeader, filter);
		return filterModel;
	}

	// Script used for dummy filters
	public static readonly byte[][] DummyScript = new byte[][]
		{
			Convert.FromHexString("0009BBE4C2D17185643765C265819BF5261755247D")
		};

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

	public static GolombRiceFilter CreateDummyEmptyFilter(uint256 blockHash)
	{
		return new GolombRiceFilterBuilder()
			.SetKey(blockHash)
			.SetP(20)
			.SetM(1 << 20)
			.AddEntries(DummyScript)
			.Build();
	}
}

public static class BlockchainInfoExtensions
{
	public static bool IsSynchronized(this BlockchainInfo me) =>
		me.Blocks == me.Headers;
}
