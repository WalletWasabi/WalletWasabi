using NBitcoin;
using NBitcoin.RPC;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.WebClients.Wasabi;

public class WasabiClient
{
	public WasabiClient(HttpClient httpClient)
	{
		_httpClient = httpClient;
	}

	private readonly HttpClient _httpClient;

	public static ushort ApiVersion { get; private set; } = ushort.Parse(Helpers.Constants.BackendMajorVersion);

	#region batch

	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public async Task<SynchronizeResponse> GetSynchronizeAsync(uint256 bestKnownBlockHash, int count, EstimateSmartFeeMode? estimateMode = null, CancellationToken cancel = default)
	{
		string relativeUri = $"api/v{ApiVersion}/btc/batch/synchronize?bestKnownBlockHash={bestKnownBlockHash}&maxNumberOfFilters={count}";
		if (estimateMode is { })
		{
			relativeUri = $"{relativeUri}&estimateSmartFeeMode={estimateMode}";
		}

		using HttpResponseMessage response = await _httpClient.GetAsync(relativeUri, cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var ret = await content.ReadAsJsonAsync(Decode.SynchronizeResponse).ConfigureAwait(false);

		return ret;
	}

	#endregion batch

	#region blockchain

	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public async Task<FiltersResponse?> GetFiltersAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancel = default)
	{
		using HttpResponseMessage response = await _httpClient.GetAsync(
			$"api/v{ApiVersion}/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}",
			cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode == HttpStatusCode.NoContent)
		{
			return null;
		}

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var ret = await content.ReadAsJsonAsync(Decode.FiltersResponse).ConfigureAwait(false);

		return ret;
	}

	public async Task<ushort> GetBackendMajorVersionAsync(CancellationToken cancel)
	{
		using HttpResponseMessage response = await _httpClient.GetAsync("api/software/versions", cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var resp = await content.ReadAsJsonAsync(Decode.VersionsResponse).ConfigureAwait(false);

		return ushort.Parse(resp.BackendMajorVersion);
	}

	public async Task<bool> CheckUpdatesAsync(CancellationToken cancel)
	{
		ushort backendMajorVersion;
		try
		{
			 backendMajorVersion = await GetBackendMajorVersionAsync(cancel).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Could not get the backend major version: {ex}");
			throw;
		}

		// If ClientSupportBackendVersionMin <= backend major <= ClientSupportBackendVersionMax, then our software is compatible.
		var backendCompatible = int.Parse(Helpers.Constants.ClientSupportBackendVersionMax) >= backendMajorVersion && backendMajorVersion >= int.Parse(Helpers.Constants.ClientSupportBackendVersionMin);
		var currentBackendMajorVersion = backendMajorVersion;

		if (backendCompatible)
		{
			// Only refresh if compatible.
			ApiVersion = currentBackendMajorVersion;
		}

		return backendCompatible;
	}

	#endregion software
}
