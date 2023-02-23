using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierApiClient
{
	public CoinVerifierApiClient(string token, HttpClient httpClient)
	{
		ApiToken = token;
		HttpClient = httpClient;
	}

	private string ApiToken { get; }

	private HttpClient HttpClient { get; }

	public virtual async Task<ApiResponseItem> SendRequestAsync(Script script, CancellationToken cancellationToken)
	{
		if (HttpClient.BaseAddress is null)
		{
			throw new HttpRequestException($"{nameof(HttpClient.BaseAddress)} was null.");
		}
		if (HttpClient.BaseAddress.Scheme != "https")
		{
			throw new HttpRequestException($"The connection to the API is not safe. Expected https but was {HttpClient.BaseAddress.Scheme}.");
		}

		var address = script.GetDestinationAddress(Network.Main); // API provider doesn't accept testnet/regtest addresses.

		int tries = 3;
		var delay = TimeSpan.FromSeconds(2);

		HttpResponseMessage? response = null;

		do
		{
			tries--;

			using var content = new HttpRequestMessage(HttpMethod.Get, $"{HttpClient.BaseAddress}{address}");
			content.Headers.Authorization = new("Bearer", ApiToken);

			try
			{
				var before = DateTimeOffset.UtcNow;

				response = await HttpClient.SendAsync(content, cancellationToken).ConfigureAwait(false);

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
			catch (Exception ex)
			{
				Logger.LogWarning($"API request failed for script: {script}. Remaining tries: {tries}. Exception: {ex}.");
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}
		while (tries > 0);

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
}
