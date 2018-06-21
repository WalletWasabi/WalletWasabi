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
using System.Threading;
using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class WasabiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public WasabiClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		#region blockchain

		/// <remarks>
		/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<FiltersResponse> GetFiltersAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancel = default)
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK,
																	$"/api/v1/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}",
																	cancel: cancel))
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
				{
					return null;
				}
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<FiltersResponse>();
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
					await response.ThrowRequestExceptionFromContentAsync();
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
					await response.ThrowRequestExceptionFromContentAsync();
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

		#endregion blockchain

		#region offchain

		public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync()
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, "/api/v1/btc/offchain/exchange-rates"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IEnumerable<ExchangeRate>>();
					return ret;
				}
			}
		}

		#endregion offchain
	}
}
