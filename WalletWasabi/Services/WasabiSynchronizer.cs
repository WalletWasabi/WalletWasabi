using NBitcoin.RPC;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class WasabiSynchronizer(
	TimeSpan period,
	int maxFiltersToSync,
	BitcoinStore bitcoinStore,
	IHttpClientFactory httpClientFactory,
	EventBus eventBus)
	: PeriodicRunner(period)
{
	private readonly SmartHeaderChain _smartHeaderChain = bitcoinStore.SmartHeaderChain;
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("long-live-satoshi-backend");

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Don't attempt synchronization without a valid tip hash
		if (_smartHeaderChain.TipHash is null)
		{
			return;
		}
		var wasabiClient = new WasabiClient(_httpClient, eventBus);
		var lastUsedApiVersion = WasabiClient.ApiVersion;

		try
		{
			var response = await wasabiClient.GetFiltersAsync(_smartHeaderChain.TipHash, maxFiltersToSync, cancel)
				.ConfigureAwait(false);

			switch (response)
			{
				case WasabiClient.FiltersResponse.AlreadyOnBestBlock:
					// Already synchronized. Nothing to do.
					return;
				case WasabiClient.FiltersResponse.BestBlockUnknown:
					// Reorg happened. Rollback the latest index.
					FilterModel reorgedFilter = await bitcoinStore.IndexStore.TryRemoveLastFilterAsync().ConfigureAwait(false)
						?? throw new InvalidOperationException("Fatal error: Failed to remove the reorged filter.");

					Logger.LogInfo($"REORG Invalid Block: {reorgedFilter.Header.BlockHash}.");
					break;
				case WasabiClient.FiltersResponse.NewFiltersAvailable newFiltersAvailable:
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

			TriggerRound();

		}
		catch (HttpRequestException ex)
		{
			if (ex.Message.Contains("Not Found"))
			{
				// Backend API version might be updated meanwhile. Trying to update the versions.
				var backendCompatible =
					await CheckBackendCompatibilityAsync(wasabiClient, cancel).ConfigureAwait(false);
				if (!backendCompatible)
				{
					eventBus.Publish(new BackendIncompatibilityDetected());
				}

				// If the backend is compatible and the Api version updated then we just used the wrong API.
				if (backendCompatible && lastUsedApiVersion != WasabiClient.ApiVersion)
				{
					// Next request will be fine, do not throw exception.
					TriggerRound();
					return;
				}
			}

			await Task.Delay(3000, cancel).ConfigureAwait(false); // Retry sooner in case of connection error.
			TriggerRound();
			throw;
		}
	}

	private static async Task<bool> CheckBackendCompatibilityAsync(WasabiClient wasabiClient, CancellationToken cancel)
	{
		bool backendCompatible;
		try
		{
			backendCompatible = await wasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
		}
		catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
		{
			// Backend is online but the endpoint for versions doesn't exist -> backend is not compatible.
			backendCompatible = false;
		}

		return backendCompatible;
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
