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
using WalletWasabi.Models;
using System.Text;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class WasabiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public WasabiClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		public async Task<Money> GetAndCalculateFeesAsync(int feeTarget, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			WasabiClient client = new WasabiClient(baseUri, torSocks5EndPoint);
			Money feePerBytes = null;

			using (var response = await client.TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v1/btc/blockchain/fees/{feeTarget}"))
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

		public async Task BroadcastTransactionAsync(SmartTransaction transaction, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			WasabiClient client = new WasabiClient(baseUri, torSocks5EndPoint);

			using (var content = new StringContent($"'{transaction.Transaction.ToHex()}'", Encoding.UTF8, "application/json"))
			using (var response = await client.TorClient.SendAsync(HttpMethod.Post, "/api/v1/btc/blockchain/broadcast", content))
			{
				if (response.StatusCode == HttpStatusCode.BadRequest)
				{
					throw new HttpRequestException($"Couldn't broadcast transaction. Reason: {await response.Content.ReadAsStringAsync()}");
				}
				if (response.StatusCode != HttpStatusCode.OK) // Try again.
				{
					throw new HttpRequestException($"Couldn't broadcast transaction. Reason: {response.StatusCode.ToReasonString()}");
				}
			}
		}
	}
}
