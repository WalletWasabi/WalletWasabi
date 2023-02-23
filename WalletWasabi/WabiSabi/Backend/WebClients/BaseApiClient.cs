using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class BaseApiClient
{
	protected BaseApiClient(HttpClient httpClient, Uri baseAddress, string token, TimeSpan? timeout = null)
	{
		HttpClient = ConfigureHttpClient(httpClient, baseAddress, token, timeout);
	}
	
	private HttpClient HttpClient { get; }
	protected Uri? BaseAddress => HttpClient.BaseAddress;
	
	private static HttpClient ConfigureHttpClient(HttpClient httpClient, Uri? baseAddress, string? token, TimeSpan? timeout)
	{
		if (baseAddress is not null)
		{
			httpClient.BaseAddress = baseAddress;
		}

		if (token is not null)
		{
			httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
		}

		if (timeout is not null)
		{
			httpClient.Timeout = timeout.Value;
		}

		return httpClient;
	}
	
	protected async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, string? statistaName = null, CancellationToken cancel = default)
	{
		var stopWatch = new Stopwatch();

		stopWatch.Start();
		var response = await HttpClient.SendAsync(request, cancel).ConfigureAwait(false);
		stopWatch.Stop();

		if (statistaName is { })
		{
			RequestTimeStatista.Instance.Add(statistaName, stopWatch.Elapsed);
		}

		return response;
	}

	protected async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response, CancellationToken cancel = default)
	{
		var responseString = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);

		return JsonConvert.DeserializeObject<T>(responseString) 
		       ?? throw new JsonSerializationException($"Failed to deserialize API response, response string was: '{responseString}'");
	}
}