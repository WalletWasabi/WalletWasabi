using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class BaseApiClient
{
	protected BaseApiClient(HttpClient httpClient)
	{
		HttpClient = httpClient;
	}
	private HttpClient HttpClient { get; }
	protected Uri? BaseAddress => HttpClient.BaseAddress;

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
