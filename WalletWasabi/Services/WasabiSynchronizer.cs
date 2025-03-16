using NBitcoin.RPC;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.WabiSabi.Client;
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
	private readonly FilterProcessor _filterProcessor = new(bitcoinStore);
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("long-live-satoshi-backend");

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Don't attempt synchronization without a valid tip hash
		if (_smartHeaderChain.TipHash is null)
		{
			return;
		}
		var wasabiClient = new WasabiClient(_httpClient);
		var lastUsedApiVersion = WasabiClient.ApiVersion;

		try
		{
			var response = await wasabiClient
				.GetSynchronizeAsync(_smartHeaderChain.TipHash, maxFiltersToSync, EstimateSmartFeeMode.Conservative, cancel)
				.ConfigureAwait(false);

			UpdateStatus(BackendStatus.Connected, TorStatus.Running);

			// If it's not fully synced or reorg happened.
			if (NeedsContinuedSynchronization(response))
			{
				TriggerRound();
			}

			await ProcessFiltersAsync(response).ConfigureAwait(false);

			eventBus.Publish(new ServerTipHeightChanged(response.BestHeight));
		}
		catch (HttpRequestException ex) when (ex.InnerException is SocketException innerEx)
		{
			bool isConnectionRefused = innerEx.SocketErrorCode == SocketError.ConnectionRefused;
			UpdateStatus(BackendStatus.NotConnected, isConnectionRefused ? TorStatus.NotRunning : TorStatus.Running);

			await Task.Delay(3000, cancel).ConfigureAwait(false); // Retry sooner in case of connection error.
			TriggerRound();
			throw;
		}
		catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
		{
			// Backend API version might be updated meanwhile. Trying to update the versions.
			var backendCompatible = await CheckBackendCompatibilityAsync(wasabiClient, cancel).ConfigureAwait(false);
			if (!backendCompatible)
			{
				eventBus.Publish(new BackendIncompatibilityDetected());
			}

			UpdateStatus(BackendStatus.NotConnected, TorStatus.Running);

			// If the backend is compatible and the Api version updated then we just used the wrong API.
			if (backendCompatible && lastUsedApiVersion != WasabiClient.ApiVersion)
			{
				// Next request will be fine, do not throw exception.
				TriggerRound();
				return;
			}

			await Task.Delay(3000, cancel).ConfigureAwait(false); // Retry sooner in case of connection error.
			TriggerRound();
			throw;
		}
		catch (Exception)
		{
			UpdateStatus(BackendStatus.NotConnected, TorStatus.Running);
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

	private void UpdateStatus(BackendStatus backendStatus, TorStatus torStatus)
	{
		eventBus.Publish(new BackendConnectionStateChanged(backendStatus));
		eventBus.Publish(new TorConnectionStateChanged(torStatus));
	}

	private bool NeedsContinuedSynchronization(SynchronizeResponse response)
	{
		return response.Filters.Count() == maxFiltersToSync ||
		       response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound;
	}

	private async Task ProcessFiltersAsync(SynchronizeResponse response)
	{
		await _filterProcessor
			.ProcessAsync((uint) response.BestHeight, response.FiltersResponseState, response.Filters)
			.ConfigureAwait(false);
	}
}
