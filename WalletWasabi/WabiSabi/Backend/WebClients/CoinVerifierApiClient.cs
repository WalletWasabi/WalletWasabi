using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Banning;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class CoinVerifierApiClient : BaseApiClient
{
	public CoinVerifierApiClient(string token, HttpClient httpClient) : base(httpClient)
	{
		ApiToken = token;
	}

	private TimeSpan TotalApiRequestTimeout { get; } = TimeSpan.FromMinutes(3);
	private string ApiToken { get; }

	public virtual async Task<ApiResponseItem> SendRequestAsync(Script script, CancellationToken cancellationToken)
	{
		if (BaseAddress is null)
		{
			throw new HttpRequestException($"{nameof(BaseAddress)} was null.");
		}
		if (BaseAddress.Scheme != "https")
		{
			throw new HttpRequestException($"The connection to the API is not safe. Expected https but was {BaseAddress.Scheme}.");
		}

		var address = script.GetDestinationAddress(Network.Main); // API provider don't accept testnet/regtest addresses.

		using CancellationTokenSource timeoutTokenSource = new(TotalApiRequestTimeout);
		using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);

		int tries = 3;
		var delay = TimeSpan.FromSeconds(2);

		HttpResponseMessage? response = null;

		do
		{
			tries--;

			using var content = new HttpRequestMessage(HttpMethod.Get, $"{BaseAddress}{address}");
			content.Headers.Authorization = new("Bearer", ApiToken);

			try
			{
				response = await base.SendRequestAsync(content, "verifier-request", linkedTokenSource.Token).ConfigureAwait(false);

				if (response.StatusCode == HttpStatusCode.OK)
				{
					// Successful request, break the iteration.
					break;
				}
				
				throw new InvalidOperationException($"Response was either null or response.{nameof(HttpStatusCode)} was {response?.StatusCode}.");
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
		if (response?.StatusCode != HttpStatusCode.OK)
		{
			throw new InvalidOperationException($"API request failed. {nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		return await DeserializeResponseAsync<ApiResponseItem>(response, linkedTokenSource.Token).ConfigureAwait(false);
	}
}
