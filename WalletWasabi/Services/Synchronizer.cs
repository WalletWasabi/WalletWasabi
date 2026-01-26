using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;
using FiltersResponse = WalletWasabi.WebClients.Wasabi.FiltersResponse;

namespace WalletWasabi.Services;


using FilterFetchingResult = Result<FiltersResponse, FilterFetchingError>;

public enum FilterFetchingError
{
	Continue,
	ContinueAfter30Seconds
}

public interface ICompactFilterProvider
{
	Task<FilterFetchingResult> GetFiltersAsync(uint256 fromHash, uint fromHeight, CancellationToken cancellationToken);
}

public class WebApiFilterProvider(int maxFiltersToSync, IHttpClientFactory httpClientFactory, EventBus eventBus) : ICompactFilterProvider
{
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("long-live-satoshi-backend");

	public async Task<FilterFetchingResult> GetFiltersAsync(uint256 fromHash, uint fromHeight, CancellationToken cancellationToken)
	{
		var wasabiClient = new IndexerClient(_httpClient, eventBus);
		var lastUsedApiVersion = IndexerClient.ApiVersion;

		try
		{
			return await wasabiClient.GetFiltersAsync(fromHash, maxFiltersToSync, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (HttpRequestException ex)
		{
			if (ex.Message.Contains("Not Found"))
			{
				// Backend API version might be updated meanwhile. Trying to update the versions.
				var backendCompatible =
					await CheckBackendCompatibilityAsync(wasabiClient, cancellationToken).ConfigureAwait(false);
				if (!backendCompatible)
				{
					eventBus.Publish(new IndexerIncompatibilityDetected());
				}

				// If the backend is compatible and the Api version updated then we just used the wrong API.
				if (backendCompatible && lastUsedApiVersion != IndexerClient.ApiVersion)
				{
					// Next request will be fine, do not throw exception.
					return FilterFetchingResult.Fail(FilterFetchingError.Continue);
				}
			}

			return FilterFetchingResult.Fail(FilterFetchingError.ContinueAfter30Seconds);
		}
	}

	private static async Task<bool> CheckBackendCompatibilityAsync(IndexerClient indexerClient, CancellationToken cancel)
	{
		bool backendCompatible;
		try
		{
			backendCompatible = await indexerClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
		}
		catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
		{
			// Backend is online but the endpoint for versions doesn't exist -> backend is not compatible.
			backendCompatible = false;
		}

		return backendCompatible;
	}
}

public class BitcoinRpcFilterProvider(IRPCClient bitcoinRpcClient) : ICompactFilterProvider
{
	public async Task<FilterFetchingResult> GetFiltersAsync(uint256 fromHash, uint fromHeight, CancellationToken cancellationToken)
	{
		var filters = new List<FilterModel>();
		var currentHeight = await bitcoinRpcClient.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);
		var nbOfFiltersToFetch = Math.Min(1_000, currentHeight - fromHeight);
		var stopAtHeight = fromHeight + nbOfFiltersToFetch;

		try
		{
			var realBlockHash = await bitcoinRpcClient.GetBlockHashAsync((int)fromHeight, cancellationToken).ConfigureAwait(false);
			if (realBlockHash != fromHash)
			{
				return new FiltersResponse.BestBlockUnknown();
			}

			var heights = Enumerable.Range((int)fromHeight + 1, (int)(stopAtHeight - fromHeight)).ToArray();

			var batchClient = bitcoinRpcClient.PrepareBatch();
			var blockHashTasks = heights.Select(h => batchClient.GetBlockHashAsync(h, cancellationToken)).ToArray();
			await batchClient.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			var blockHashes = await Task.WhenAll(blockHashTasks).ConfigureAwait(false);

			var filterBatchClient = bitcoinRpcClient.PrepareBatch();
			var filterTasks = blockHashes.Select(hash => filterBatchClient.GetBlockFilterAsync(hash, cancellationToken)).ToArray();
			await filterBatchClient.SendBatchAsync(cancellationToken).ConfigureAwait(false);
			var filterResponses = await Task.WhenAll(filterTasks).ConfigureAwait(false);

			for (var i = 0; i < blockHashes.Length; i++)
			{
				var blockHash = blockHashes[i];
				var filterResponse = filterResponses[i];
				var height = (uint)heights[i];

				var filter = new FilterModel(
					new SmartHeader(blockHash, filterResponse.Header, height, DateTimeOffset.UtcNow),
					filterResponse.Filter);

				filters.Add(filter);
			}

			return filters.Count == 0
				? new FiltersResponse.AlreadyOnBestBlock()
				: new FiltersResponse.NewFiltersAvailable(currentHeight, filters.ToArray());
		}
		catch (RPCException e) when (e.RPCCode == RPCErrorCode.RPC_INVALID_PARAMETER) // Block height out of range
		{
			return new FiltersResponse.BestBlockUnknown();
		}
	}
}

public static class Synchronizer
{
	public static MessageHandler<Unit> CreateFilterGenerator(ICompactFilterProvider filtersProvider, BitcoinStore bitcoinStore, EventBus eventBus) =>
		(_, cancellationToken) => GenerateCompactFiltersAsync(filtersProvider, bitcoinStore, eventBus, cancellationToken);

	private static async Task<Unit> GenerateCompactFiltersAsync(ICompactFilterProvider filtersProvider, BitcoinStore bitcoinStore, EventBus eventBus, CancellationToken cancellationToken)
	{
		var smartHeaderChain = bitcoinStore.SmartHeaderChain;

		// Don't attempt synchronization without a valid tip hash
		if (smartHeaderChain.TipHash is null)
		{
			await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken).ConfigureAwait(false);
			return Unit.Instance;
		}

		var response = await filtersProvider.GetFiltersAsync(smartHeaderChain.TipHash, smartHeaderChain.TipHeight, cancellationToken)
			.ConfigureAwait(false);

		if (response.IsOk)
		{
			var isSynchronized = await ProcessFiltersAsync(response.Value, bitcoinStore, eventBus).ConfigureAwait(false);
			if (isSynchronized)
			{
				await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
			}
		}
		else
		{
			var continueAfterSeconds = response.Error == FilterFetchingError.ContinueAfter30Seconds ? 30 : 0;
			await Task.Delay(TimeSpan.FromSeconds(continueAfterSeconds), cancellationToken).ConfigureAwait(false);
		}
		return Unit.Instance;
	}

	private static async Task<bool> ProcessFiltersAsync(FiltersResponse response, BitcoinStore bitcoinStore, EventBus eventBus)
	{
		switch (response)
		{
			case FiltersResponse.AlreadyOnBestBlock:
				// Already synchronized. Nothing to do.
				var tip = bitcoinStore.SmartHeaderChain.TipHeight;
				bitcoinStore.SmartHeaderChain.SetServerTipHeight(tip);
				eventBus.Publish(new ServerTipHeightChanged((int)tip));
				return true;
			case FiltersResponse.BestBlockUnknown:
				// Reorg happened. Rollback the latest index.
				FilterModel reorgedFilter = await bitcoinStore.FilterStore.TryRemoveLastFilterAsync().ConfigureAwait(false)
					?? throw new InvalidOperationException("Fatal error: Failed to remove the reorged filter.");

				Logger.LogInfo($"REORG Invalid Block: {reorgedFilter.Header.BlockHash}  Height {reorgedFilter.Header.Height}.");
				break;
			case FiltersResponse.NewFiltersAvailable newFiltersAvailable:
				var hashChain = bitcoinStore.SmartHeaderChain;
				hashChain.SetServerTipHeight((uint)newFiltersAvailable.BestHeight);
				eventBus.Publish(new ServerTipHeightChanged(newFiltersAvailable.BestHeight));
				var filters = newFiltersAvailable.Filters;
				var firstFilter = filters.First();
				if (hashChain.TipHeight + 1 != firstFilter.Header.Height)
				{
					// We have a problem.
					// We have wrong filters, the heights are not in sync with the server's.
					Logger.LogError($"Inconsistent index state detected.{Environment.NewLine}" + FormatInconsistencyDetails(hashChain, firstFilter));

					await bitcoinStore.FilterStore.RemoveAllNewerThanAsync(hashChain.TipHeight).ConfigureAwait(false);
				}
				else
				{
					await bitcoinStore.FilterStore.AddNewFiltersAsync(filters).ConfigureAwait(false);

					Logger.LogInfo(filters.Length == 1
						? $"Downloaded filter for block {firstFilter.Header.Height}."
						: $"Downloaded filters for blocks from {firstFilter.Header.Height} to {filters.Last().Header.Height}.");
				}
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(response));
		}

		return false;
	}


	private static string FormatInconsistencyDetails(SmartHeaderChain hashChain, FilterModel firstFilter)
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
