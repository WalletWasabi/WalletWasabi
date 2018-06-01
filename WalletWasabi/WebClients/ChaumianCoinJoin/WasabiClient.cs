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
using NBitcoin.RPC;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class WasabiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public WasabiClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		#region blockchain

		public async Task<IEnumerable<FilterModel>> GetFiltersAsync(uint256 bestKnownBlockHash, Height blockHeight, int count)
		{
			using(var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK, 
			                                                       $"/api/v1/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}"))
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
				{
					return Enumerable.Empty<FilterModel>();
				}
				if (response.StatusCode != HttpStatusCode.OK)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}

				using(HttpContent content = response.Content)
				{
					var filters = (await content.ReadAsJsonAsync<IEnumerable<string>>()).ToList();

					var ret = filters.Select(s => FilterModel.FromLine(s, blockHeight + filters.IndexOf(s) + 1));
					return ret;
				}
			}
		}

		public async Task<IDictionary<int, FeeEstimationPair>> GetFeesAsync(params int[] confirmationTargets)
		{
			var confirmationTargetsString = string.Join(",", confirmationTargets);

			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v1/btc/blockchain/fees/{confirmationTargetsString}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IDictionary<int, FeeEstimationPair>>();
					return ret;
				}
			}
		}

		public async Task BroadcastAsync(string hex)
		{
			using (var content = new StringContent($"'{hex}'", Encoding.UTF8, "application/json"))
			using (var response = await TorClient.SendAsync(HttpMethod.Post, "/api/v1/btc/blockchain/broadcast", content))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}
			}
		}

		public async Task BroadcastAsync(Transaction transaction)
		{
			await BroadcastAsync(transaction.ToHex());
		}

		public async Task BroadcastAsync(SmartTransaction transaction)
		{
			await BroadcastAsync(transaction.Transaction);
		}

		#endregion

		#region offchain

		public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync()
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, "/api/v1/btc/offchain/exchange-rates"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}

				using(HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IEnumerable<ExchangeRate>>();
					return ret;
				}
			}
		}

		#endregion
	}
}
