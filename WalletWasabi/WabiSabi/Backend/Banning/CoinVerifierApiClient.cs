using NBitcoin;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierApiClient : IAsyncDisposable
{
	/// <summary>Maximum number of actual HTTP requests that might be served concurrently by the CoinVerifier webserver.</summary>
	public const int MaxParallelRequestCount = 30;

	/// <summary>Maximum re-tries for a single API request.</summary>
	private const int MaxRetries = 3;

	public CoinVerifierApiClient(string apiToken, HttpClient httpClient)
	{
		ApiToken = apiToken;
		HttpClient = httpClient;

		if (HttpClient.BaseAddress is null)
		{
			throw new HttpRequestException($"{nameof(HttpClient.BaseAddress)} was null.");
		}

		if (HttpClient.BaseAddress.Scheme != "https")
		{
			throw new HttpRequestException($"The connection to the API is not safe. Expected https but was {HttpClient.BaseAddress.Scheme}.");
		}
	}

	/// <summary>Long timeout for a single API request. No retry after that. </summary>
	private static TimeSpan ApiRequestTimeout { get; } = TimeSpan.FromMinutes(5);

	private string ApiToken { get; }

	private HttpClient HttpClient { get; }

	private SemaphoreSlim ThrottlingSemaphore { get; } = new(initialCount: MaxParallelRequestCount);

	public virtual async Task<ApiResponseItem> SendRequestAsync(Script script, CancellationToken cancellationToken)
	{
		var address = script.GetDestinationAddress(Network.Main); // API provider doesn't accept testnet/regtest addresses.

		HttpResponseMessage? response = null;

		for (int i = 0; i < MaxRetries; i++)
		{
			try
			{
				using var content = new HttpRequestMessage(HttpMethod.Get, $"{HttpClient.BaseAddress}{address}");
				content.Headers.Authorization = new("Bearer", ApiToken);

				// Makes sure that there are no more than MaxParallelRequestCount requests in-flight at a time.
				// Re-tries are not an exception to the max throttling limit.
				await ThrottlingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
				var before = DateTimeOffset.UtcNow;
				try
				{
					using CancellationTokenSource apiTimeoutCts = new(ApiRequestTimeout);
					using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(apiTimeoutCts.Token, cancellationToken);
					response = await HttpClient.SendAsync(content, linkedCts.Token).ConfigureAwait(false);
				}
				finally
				{
					ThrottlingSemaphore.Release();
				}

				var duration = DateTimeOffset.UtcNow - before;
				RequestTimeStatista.Instance.Add("verifier-request", duration);

				if (response is { } && response.StatusCode == HttpStatusCode.OK)
				{
					// Successful request, break the iteration.
					break;
				}
				else
				{
					throw new InvalidOperationException($"Response was either null or response.{nameof(HttpStatusCode)} was {response?.StatusCode}.");
				}
			}
			catch (OperationCanceledException)
			{
				Logger.LogWarning($"API request timed out for script: {script}.");
				throw;
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"API request failed for script: {script}. Remaining tries: {i}. Exception: {ex}.");
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
			}
		}

		// Throw proper exceptions - if needed - according to the latest response.
		if (response?.StatusCode == HttpStatusCode.Forbidden)
		{
			throw new UnauthorizedAccessException("User roles access forbidden.");
		}
		else if (response?.StatusCode != HttpStatusCode.OK)
		{
			throw new InvalidOperationException($"API request failed. {nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		string responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		ApiResponseItem deserializedRecord = JsonConvert.DeserializeObject<ApiResponseItem>(responseString)
			?? throw new JsonSerializationException($"Failed to deserialize API response, response string was: '{responseString}'");
		return deserializedRecord;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		ThrottlingSemaphore.Dispose();

		return ValueTask.CompletedTask;
	}
}
