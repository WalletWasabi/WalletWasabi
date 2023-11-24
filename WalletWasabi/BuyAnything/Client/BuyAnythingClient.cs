using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.BuyAnything.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;

public class BuyAnythingApiClient
{

	public BuyAnythingApiClient(IHttpClient httpClient)
	{
		HttpClient = httpClient;
	}

	private IHttpClient HttpClient { get; }

	public async Task SendAsync(BuyAnythingMessage message, CancellationToken cancel)
	{
		string endpoint = "/api/BuyAnything/send";
		string jsonContent = JsonConvert.SerializeObject(message);
		using StringContent content = new (jsonContent, Encoding.UTF8, "application/json");

		HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Post, endpoint, content, cancellationToken: cancel).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException($"Failed to send request. Status code: {response.StatusCode}");
		}
	}

	public async Task<IEnumerable<BuyAnythingMessage>> GetRepliesAsync(string requestId, long since, CancellationToken cancel)
	{
		string endpoint = $"/api/BuyAnything/getreplies?requestId={requestId}&since={since}";
		HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken: cancel).ConfigureAwait(false);

		if (response.IsSuccessStatusCode)
		{
			using HttpContent content = response.Content;
			var replies = await content.ReadAsJsonAsync<IEnumerable<BuyAnythingMessage>>().ConfigureAwait(false);
			return replies;
		}

		throw new HttpRequestException($"Failed to get replies. Status code: {response.StatusCode}");
	}
}
