using System.Diagnostics;
using NBitcoin;
using NBitcoin.RPC;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Services;


using FilterFetchingResult = Result<FiltersResponse, TimeSpan>;

public abstract record FiltersResponse
{
	public record AlreadyOnBestBlock : FiltersResponse;
	public record BestBlockUnknown : FiltersResponse;
	public record NewFiltersAvailable(ChainHeight BestHeight, FilterModel[] Filters) : FiltersResponse;
}

public delegate Task<FilterFetchingResult> FilterProvider(uint fromHeight, uint256 fromHash, CancellationToken cancellationToken);

public static class FilterProviders
{
	private static readonly FiltersResponse.AlreadyOnBestBlock AlreadyOnBestBlock = new();
	private static readonly FiltersResponse.BestBlockUnknown BestBlockUnknown = new();
	private static FiltersResponse.NewFiltersAvailable NewFiltersAvailable(ChainHeight bestHeight, FilterModel[] filters) => new(bestHeight, filters);

	public static FilterProvider CreateBitcoinRpcFilterProvider(IRPCClient bitcoinClient, ConcurrentChain blockHeaderChain) =>
		(fromHeight, fromHash, cancellationToken) => GetFiltersFromBitcoinRpcAsync(bitcoinClient, blockHeaderChain, fromHash, fromHeight, cancellationToken);

	private static async Task<FilterFetchingResult> GetFiltersFromBitcoinRpcAsync(IRPCClient bitcoinRpcClient, ConcurrentChain blockHeaderChain, uint256 fromHash, uint fromHeight, CancellationToken cancellationToken)
	{
		try
		{
			var blockHashes = await GetBlockHashesAsync(bitcoinRpcClient, blockHeaderChain, fromHash, fromHeight, cancellationToken).ConfigureAwait(false);
			if (blockHashes.Length == 0)
			{
				return AlreadyOnBestBlock;
			}

			var filterBatchClient = bitcoinRpcClient.PrepareBatch();
			var filterTasks = blockHashes.Select(hash => filterBatchClient.GetBlockFilterAsync(hash, cancellationToken))
				.ToArray();
			await filterBatchClient.SendBatchAsync(cancellationToken).ConfigureAwait(false);
			var filterResponses = await Task.WhenAll(filterTasks).ConfigureAwait(false);

			var filters = new FilterModel[blockHashes.Length];
			var height = fromHeight + 1;
			for (var i = 0; i < blockHashes.Length; i++)
			{
				var blockHash = blockHashes[i];
				var filterResponse = filterResponses[i];

				var header = new SmartHeader(blockHash, filterResponse.Header, height, DateTimeOffset.UtcNow);
				var filter = new FilterModel(header, filterResponse.Filter);

				filters[i] = filter;
				height++;
			}

			return NewFiltersAvailable(height, filters);
		}
		catch (RPCException e) when (e.RPCCode == RPCErrorCode.RPC_INVALID_PARAMETER) // Block height out of range
		{
			return BestBlockUnknown;
		}
		catch (Exception e)
		{
			var msg = e is HttpRequestException {InnerException: SocketException}
				? "Cannot connect to get filter from bitcoin RPC"
				: "Error fetching filter from bitcoin RPC";
			Logger.LogError($"{msg} - {e.Message}. Retrying in 15 seconds...");
			return FilterFetchingResult.Fail(TimeSpan.FromSeconds(15));
		}
	}

	private static async Task<uint256[]> GetBlockHashesAsync(IRPCClient bitcoinRpcClient,
		ConcurrentChain blockHeaderChain, uint256 fromHash, uint fromHeight, CancellationToken cancellationToken)
	{
		if (blockHeaderChain.Tip?.Height > fromHeight)
		{
			return blockHeaderChain
				.EnumerateAfter(fromHash)
				.Select(x => x.HashBlock)
				.Take(1_000)
				.ToArray();
		}

		var currentHeight = await bitcoinRpcClient.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);
		var nbOfFiltersToFetch = Math.Min(1_000, currentHeight - fromHeight);
		if (nbOfFiltersToFetch == 0)
		{
			return [];
		}

		var heights = Enumerable.Range((int) fromHeight + 1, (int) nbOfFiltersToFetch).ToArray();
		var batchClient = bitcoinRpcClient.PrepareBatch();
		var blockHashTasks = heights.Select(h => batchClient.GetBlockHashAsync(h, cancellationToken)).ToArray();
		await batchClient.SendBatchAsync(cancellationToken).ConfigureAwait(false);

		var blockHashes = await Task.WhenAll(blockHashTasks).ConfigureAwait(false);
		return blockHashes;
	}

	private static async Task<FilterFetchingResult> GetFiltersFromBitcoinP2pAsync(ConcurrentChain headersChain,
		NodesGroup nodes, uint256 fromHash, uint fromHeight, CancellationToken cancellationToken)
	{
		var currentHeight = headersChain.Tip.Height;
		var nbOfFiltersToFetch = Math.Min(1_000, currentHeight - fromHeight);
		var stopAtHeight = fromHeight + nbOfFiltersToFetch;
		var stopAtHash = headersChain.GetBlock((int) stopAtHeight).HashBlock;

		var node = nodes.ConnectedNodes.RandomElement(SecureRandom.Instance)!;
		var filters = await node.GetFiltersAsync(fromHeight, stopAtHash, cancellationToken).ConfigureAwait(false);
		Debug.Assert(filters.Length == nbOfFiltersToFetch);

		var ret = new FilterModel[nbOfFiltersToFetch];
		var prevHeader = uint256.Zero;
		foreach (var (f, i) in filters.Select((f, i) => (f, i)))
		{
			var grfilter = new GolombRiceFilter(f.FilterBytes);
			var grheader = grfilter.GetHeader(prevHeader);
			var header = new SmartHeader(f.BlockHash, grheader, (uint) (currentHeight + i), DateTimeOffset.UtcNow);
			var filter = new FilterModel(header, grfilter);
			prevHeader = grheader;
			ret[i] = filter;
		}

		return NewFiltersAvailable((uint) currentHeight, ret);
	}
}

public static class Synchronizer
{
	public static MessageHandler<Unit> CreateFilterGenerator(FilterProvider filtersProvider, FilterStore filterStore, FilterHeaderChain filterHeaderChain, EventBus eventBus) =>
		(_, cancellationToken) => GenerateCompactFiltersAsync(filtersProvider, filterStore, filterHeaderChain, eventBus, cancellationToken);

	private static async Task<Unit> GenerateCompactFiltersAsync(FilterProvider filtersProvider, FilterStore filterStore, FilterHeaderChain filterHeaderChain, EventBus eventBus, CancellationToken cancellationToken)
	{
		// Don't attempt synchronization without a valid tip hash
		if (filterHeaderChain.TipHash is null)
		{
			await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken).ConfigureAwait(false);
			return Unit.Instance;
		}

		var response = await filtersProvider(filterHeaderChain.TipHeight, filterHeaderChain.TipHash, cancellationToken)
			.ConfigureAwait(false);

		if (response.IsOk)
		{
			var isSynchronized = await ProcessFiltersAsync(response.Value, filterStore, filterHeaderChain, eventBus).ConfigureAwait(false);
			if (isSynchronized)
			{
				await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
			}
		}
		else
		{
			var continueAfterSeconds = response.Error;
			await Task.Delay(continueAfterSeconds, cancellationToken).ConfigureAwait(false);
		}
		return Unit.Instance;
	}

	private static async Task<bool> ProcessFiltersAsync(FiltersResponse response, FilterStore filterStore, FilterHeaderChain filterHeaderChain, EventBus eventBus)
	{
		switch (response)
		{
			case FiltersResponse.AlreadyOnBestBlock:
				// Already synchronized. Nothing to do.
				var tip = filterHeaderChain.TipHeight;
				filterHeaderChain.SetServerTipHeight(tip);
				eventBus.Publish(new NetworkTipHeightChanged(tip));
				return true;
			case FiltersResponse.BestBlockUnknown:
				// Reorg happened. Rollback the latest index.
				FilterModel reorgedFilter = await filterStore.TryRemoveLastFilterAsync().ConfigureAwait(false)
					?? throw new InvalidOperationException("Fatal error: Failed to remove the reorged filter.");

				Logger.LogInfo($"REORG Invalid Block: {reorgedFilter.Header.BlockHash}  Height {reorgedFilter.Header.Height}.");
				break;
			case FiltersResponse.NewFiltersAvailable newFiltersAvailable:
				var localTipHeight = filterHeaderChain.TipHeight;

				filterHeaderChain.SetServerTipHeight(newFiltersAvailable.BestHeight);
				eventBus.Publish(new NetworkTipHeightChanged(newFiltersAvailable.BestHeight));

				var downloadedFilters = newFiltersAvailable.Filters;
				var newFilters = downloadedFilters.Where(x => localTipHeight < x.Header.Height).ToArray();
				var firstNewFilter = newFilters.FirstOrDefault();

				if (firstNewFilter is null)
				{
					Logger.LogInfo(downloadedFilters.Length == 1
						? $"Downloaded filter for block {downloadedFilters[0].Header.Height} is known locally."
						: $"Downloaded filters for blocks from {downloadedFilters[0].Header.Height} to {downloadedFilters[^1].Header.Height} are known locally.");
				}
				else if (localTipHeight + 1 != firstNewFilter.Header.Height)
				{
					// We have a problem.
					// We have wrong filters, the heights are not in sync with the server's.
					string details = FormatInconsistencyDetails(filterHeaderChain, firstNewFilter);
					Logger.LogError($"Inconsistent index state detected.{Environment.NewLine}{details}");

					await filterStore.RemoveAllNewerThanAsync(localTipHeight).ConfigureAwait(false);
				}
				else
				{
					await filterStore.AddNewFiltersAsync(newFilters).ConfigureAwait(false);

					Logger.LogInfo(newFilters.Length == 1
						? $"Downloaded filter for block {firstNewFilter.Header.Height}."
						: $"Downloaded filters for blocks from {firstNewFilter.Header.Height} to {newFilters.Last().Header.Height}.");
				}

				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(response));
		}

		return false;
	}

	private static string FormatInconsistencyDetails(FilterHeaderChain hashChain, FilterModel firstFilter)
	{
		return string.Join(
			Environment.NewLine,
			[
				$"  Local Chain:",
				$"    Tip Height: {hashChain.TipHeight}",
				$"    Tip Hash: {hashChain.TipHash}",
				$"    Hashes Left: {hashChain.HashesLeft}",
				$"    Hash Count: {hashChain.HashCount}",
				$"  Server:",
				$"    Server Tip Height: {hashChain.ServerTipHeight}",
				$"  First Filter:",
				$"    Block Hash: {firstFilter.Header.BlockHash}",
				$"    Height: {firstFilter.Header.Height}"
			]);
	}
}
