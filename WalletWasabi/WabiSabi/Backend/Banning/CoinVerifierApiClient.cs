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

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierApiClient
{
	public CoinVerifierApiClient(string token, Network network, HttpClient httpClient)
	{
		ApiToken = token;
		Network = network;
		HttpClient = httpClient;
	}

	public CoinVerifierApiClient() : this("", Network.Main, new() { BaseAddress = new("https://www.test.test") })
	{
	}

	private TimeSpan ApiRequestTimeout { get; } = TimeSpan.FromMinutes(2);

	private string ApiToken { get; set; }
	private Network Network { get; set; }

	private HttpClient HttpClient { get; set; }

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

		var address = script.GetDestinationAddress(Network.Main); // API provider don't accept testnet/regtest addresses.

		using CancellationTokenSource timeoutTokenSource = new(ApiRequestTimeout);
		using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);
		using var content = new HttpRequestMessage(HttpMethod.Get, $"{HttpClient.BaseAddress}{address}");
		content.Headers.Authorization = new("Bearer", ApiToken);

		int tries = 3;

		HttpResponseMessage? response = null;

		do
		{
			tries--;
			response = await HttpClient.SendAsync(content, linkedTokenSource.Token).ConfigureAwait(false);

			if (response.StatusCode == HttpStatusCode.OK)
			{
				// Successful request, break the iteration.
				break;
			}
			else
			{
				Logger.LogWarning($"API request failed. {nameof(HttpStatusCode)} was {response.StatusCode}.");
			}
		}
		while (tries > 0);

		// Throw proper exceptions - if needed - according to the latest response.
		if (response.StatusCode == HttpStatusCode.Forbidden)
		{
			throw new UnauthorizedAccessException("User roles access forbidden.");
		}
		else if (response.StatusCode != HttpStatusCode.OK)
		{
			throw new InvalidOperationException($"API request failed. {nameof(HttpStatusCode)} was {response.StatusCode}.");
		}

		string responseString = await response.Content.ReadAsStringAsync(linkedTokenSource.Token).ConfigureAwait(false);

		ApiResponseItem deserializedRecord = JsonConvert.DeserializeObject<ApiResponseItem>(responseString)
			?? throw new JsonSerializationException($"Failed to deserialize API response, response string was: '{responseString}'");
		return deserializedRecord;
	}
}
