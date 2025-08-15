using NBitcoin;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WebClients.BlockstreamInfo;

public class BlockstreamInfoClient
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly string uriString;

	public BlockstreamInfoClient(Network network, IHttpClientFactory httpClientFactory)
	{
		_httpClientFactory = httpClientFactory;
		if (httpClientFactory is OnionHttpClientFactory)
		{
			uriString = network == Network.TestNet
				? "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/testnet"
				: "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion";
		}
		else
		{
			uriString = network == Network.TestNet
				? "https://blockstream.info/testnet"
				: "https://blockstream.info";
		}
	}

	public async Task<AllFeeEstimate> GetFeeEstimatesAsync(CancellationToken cancel)
	{
		var httpClient = _httpClientFactory.CreateClient("blockstream.info");
		httpClient.BaseAddress = new Uri(uriString);
		using HttpResponseMessage response = await httpClient.GetAsync("api/fee-estimates", cancel).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		var responseString = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
		var parsed = JsonDocument.Parse(responseString).RootElement;
		var myDic = new Dictionary<int, FeeRate>();
		foreach (var elem in parsed.EnumerateObject())
		{
			myDic.Add(int.Parse(elem.Name), new FeeRate(elem.Value.GetDecimal()));
		}

		return new AllFeeEstimate(myDic);
	}
}
