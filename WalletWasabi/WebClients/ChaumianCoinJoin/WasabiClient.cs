using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Services;
using WalletWasabi.TorSocks5;
using WalletWasabi.Bases;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class WasabiClient : TorDisposableSupport
    {
		public IndexDownloader IndexDownloader { get; }

		public WasabiClient(IndexDownloader indexDownloader, Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
        {
			IndexDownloader = indexDownloader;
        }

		public async Task<Money> GetAndCalculateFeesAsync(int feeTarget)
		{
			Money feePerBytes = null;
			using (var torClient = new TorHttpClient(IndexDownloader.Client.DestinationUri, IndexDownloader.Client.TorSocks5EndPoint, isolateStream: true))
			using (var response = await torClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v1/btc/blockchain/fees/{feeTarget}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
					throw new HttpRequestException($"Couldn't query network fees. Reason: {response.StatusCode.ToReasonString()}");

				using (var content = response.Content)
				{
					var json = await content.ReadAsJsonAsync<SortedDictionary<int, FeeEstimationPair>>();
					feePerBytes = new Money(json.Single().Value.Conservative);

					return feePerBytes;
				}
			}
		}
	}
}
