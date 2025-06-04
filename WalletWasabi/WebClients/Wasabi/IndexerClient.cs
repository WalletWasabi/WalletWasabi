using System.Linq;
using NBitcoin;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.Services;

namespace WalletWasabi.WebClients.Wasabi;

public abstract record FiltersResponse
{
	public record AlreadyOnBestBlock : FiltersResponse;

	public record BestBlockUnknown : FiltersResponse;

	public record NewFiltersAvailable(int BestHeight, FilterModel[] Filters) : FiltersResponse;
}

public class IndexerClient
{
	public IndexerClient(HttpClient httpClient, EventBus? eventBus = null)
	{
		_httpClient = httpClient;
		_eventBus = eventBus ?? new EventBus();
	}

	private readonly HttpClient _httpClient;
	private readonly EventBus _eventBus;

	public static ushort ApiVersion { get; private set; } = ushort.Parse(Helpers.Constants.IndexerMajorVersion);

	public async Task<FiltersResponse> GetFiltersAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancel = default)
	{
		using HttpResponseMessage response = await _httpClient.GetAsync(
			$"api/v{ApiVersion}/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}",
			cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode == HttpStatusCode.NoContent)
		{
			_eventBus.Publish(new IndexerAvailabilityStateChanged(true));
			return new FiltersResponse.AlreadyOnBestBlock();
		}

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			_eventBus.Publish(new IndexerAvailabilityStateChanged(true));
			return new FiltersResponse.BestBlockUnknown();
		}

		await CheckErrorsAsync(response, cancel).ConfigureAwait(false);

		using HttpContent content = response.Content;
		var ret = await content.ReadAsJsonAsync(Decode.FiltersResponse).ConfigureAwait(false);

		return new FiltersResponse.NewFiltersAvailable(ret.BestHeight, ret.Filters.ToArray());
	}

	public async Task<ushort> GetIndexerMajorVersionAsync(CancellationToken cancel)
	{
		using HttpResponseMessage response = await _httpClient.GetAsync("api/software/versions", cancellationToken: cancel).ConfigureAwait(false);

		await CheckErrorsAsync(response, cancel).ConfigureAwait(false);

		using HttpContent content = response.Content;
		var resp = await content.ReadAsJsonAsync(Decode.VersionsResponse).ConfigureAwait(false);

		return ushort.Parse(resp.IndexerMajorVersion);
	}

	public async Task<bool> CheckUpdatesAsync(CancellationToken cancel)
	{
		ushort indexerMajorVersion;
		try
		{
			 indexerMajorVersion = await GetIndexerMajorVersionAsync(cancel).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Could not get the indexer major version: {ex}");
			throw;
		}

		// If ClientSupportIndexerVersionMin <= indexer major <= ClientSupportIndexerVersionMax, then our software is compatible.
		var indexerCompatible = int.Parse(Helpers.Constants.ClientSupportIndexerVersionMax) >= indexerMajorVersion && indexerMajorVersion >= int.Parse(Helpers.Constants.ClientSupportIndexerVersionMin);
		var currentIndexerMajorVersion = indexerMajorVersion;

		if (indexerCompatible)
		{
			// Only refresh if compatible.
			ApiVersion = currentIndexerMajorVersion;
		}

		return indexerCompatible;
	}

	private async Task CheckErrorsAsync(HttpResponseMessage response, CancellationToken cancel)
	{
		_eventBus.Publish(new IndexerAvailabilityStateChanged(response.StatusCode == HttpStatusCode.OK));
		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}
	}
}
