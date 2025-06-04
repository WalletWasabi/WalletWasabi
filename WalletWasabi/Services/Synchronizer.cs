using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;
using FiltersResponse = WalletWasabi.WebClients.Wasabi.FiltersResponse;

namespace WalletWasabi.Services;


using FilterFetchingResult = Helpers.Result<FiltersResponse,FilterFetchingError>;

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
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("long-live-satoshi-indexer");

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
				// Indexer API version might be updated meanwhile. Trying to update the versions.
				var indexerCompatible =
					await CheckIndexerCompatibilityAsync(wasabiClient, cancellationToken).ConfigureAwait(false);
				if (!indexerCompatible)
				{
					eventBus.Publish(new IndexerIncompatibilityDetected());
				}

				// If the indexer is compatible and the Api version updated then we just used the wrong API.
				if (indexerCompatible && lastUsedApiVersion != IndexerClient.ApiVersion)
				{
					// Next request will be fine, do not throw exception.
					return FilterFetchingResult.Fail(FilterFetchingError.Continue);
				}
			}

			return FilterFetchingResult.Fail(FilterFetchingError.ContinueAfter30Seconds);
		}
	}

	private static async Task<bool> CheckIndexerCompatibilityAsync(IndexerClient indexerClient, CancellationToken cancel)
	{
		bool indexerCompatible;
		try
		{
			indexerCompatible = await indexerClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
		}
		catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
		{
			// Indexer is online but the endpoint for versions doesn't exist -> indexer is not compatible.
			indexerCompatible = false;
		}

		return indexerCompatible;
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
			var realBlockHash = await bitcoinRpcClient.GetBlockHashAsync((int) fromHeight, cancellationToken) .ConfigureAwait(false);
			if (realBlockHash != fromHash)
			{
				return new FiltersResponse.BestBlockUnknown();
			}

			for (var height = fromHeight + 1; height <= stopAtHeight; height++)
			{
				var blockHash = await bitcoinRpcClient.GetBlockHashAsync((int) height, cancellationToken)
					.ConfigureAwait(false);
				var filterResponse = await bitcoinRpcClient.GetBlockFilterAsync(blockHash, cancellationToken)
					.ConfigureAwait(false);

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

public class Synchronizer(TimeSpan period, ICompactFilterProvider filtersProvider, BitcoinStore bitcoinStore, EventBus eventBus)
	: PeriodicRunner(period)
{
	private readonly SmartHeaderChain _smartHeaderChain = bitcoinStore.SmartHeaderChain;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Don't attempt synchronization without a valid tip hash
		if (_smartHeaderChain.TipHash is null)
		{
			return;
		}

		var response =  await filtersProvider.GetFiltersAsync(_smartHeaderChain.TipHash, _smartHeaderChain.TipHeight, cancel)
			.ConfigureAwait(false);

		if (response.IsOk)
		{
			var isSynchronized = await ProcessFiltersAsync(response.Value).ConfigureAwait(false);
			if (isSynchronized)
			{
				return;
			}
		}
		else
		{
			var continueAfterSeconds = response.Error == FilterFetchingError.ContinueAfter30Seconds ? 30 : 0;
			await Task.Delay(TimeSpan.FromSeconds(continueAfterSeconds), cancel).ConfigureAwait(false);
		}
		TriggerRound();
	}

	private async Task<bool> ProcessFiltersAsync(FiltersResponse response)
	{
		switch (response)
		{
			case FiltersResponse.AlreadyOnBestBlock:
				// Already synchronized. Nothing to do.
				var tip = _smartHeaderChain.TipHeight;
				_smartHeaderChain.SetServerTipHeight(tip);
				eventBus.Publish(new ServerTipHeightChanged((int) tip));
				return true;
			case FiltersResponse.BestBlockUnknown:
				// Reorg happened. Rollback the latest index.
				FilterModel reorgedFilter = await bitcoinStore.IndexStore.TryRemoveLastFilterAsync().ConfigureAwait(false)
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
					Logger.LogError($"Inconsistent index state detected.{Environment.NewLine}" +
					                FormatInconsistencyDetails(hashChain, firstFilter));

					await bitcoinStore.IndexStore.RemoveAllNewerThanAsync(hashChain.TipHeight).ConfigureAwait(false);
				}
				else
				{
					await bitcoinStore.IndexStore.AddNewFiltersAsync(filters).ConfigureAwait(false);

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
