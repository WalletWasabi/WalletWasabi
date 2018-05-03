using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Services;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.WebClients
{
    public class WasabiClient : IDisposable
    {
        public readonly TorHttpClient _torClient;

        public WasabiClient(TorHttpClient torClient)
        {
            _torClient = torClient;
        }

        public async Task<IEnumerable<Money>> GetFeePerByteAsync(params int[] confirmationTargets)
        {
            var ct = string.Join(",", confirmationTargets);
			using (var response = await _torClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v1/btc/blockchain/fees/{ct}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
					throw new HttpRequestException($"Couldn't query network fees. Reason: {response.StatusCode.ToReasonString()}");

				using (var content = response.Content)
				{
					var json = await content.ReadAsJsonAsync<SortedDictionary<int, FeeEstimationPair>>();
					return json.Select(x=> Money.Satoshis(x.Value.Conservative));
				}
			}
        }

        public async Task<IEnumerable<string>> GetFiltersAsync(uint256 bestKnownBlockHash, int count)
        {
			using (var response = await _torClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v1/btc/blockchain/filters/{bestKnownBlockHash}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
					throw new HttpRequestException($"Couldn't query network fees. Reason: {response.StatusCode.ToReasonString()}");

				using (var content = response.Content)
				{
					var json = await content.ReadAsJsonAsync<List<string>>();
					return json.Select(x=>x);
				}
			}
        }

        public async Task BroadcastAsync(Transaction tx)
        {
            var content = new StringContent($"'{tx.ToHex()}'", System.Text.Encoding.UTF8, "application/json");
			using (var response = await _torClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/blockchain/broadcast", content))
			{
				if (response.StatusCode != HttpStatusCode.OK)
                {
					string message = await response.Content.ReadAsJsonAsync<string>();
					throw new HttpRequestException(message);
                }
			}
        }

        public async Task<InputsResponse> RegisterInputAsync(InputsRequest request)
        {
			using (var response = await _torClient.SendAsync(HttpMethod.Post, "/api/v1/btc/chaumiancoinjoin/inputs/", request.ToHttpStringContent()))
			{
				if (response.StatusCode != HttpStatusCode.OK)
                {
					string message = await response.Content.ReadAsJsonAsync<string>();
					throw new HttpRequestException(message);
                }
                return await response.Content.ReadAsJsonAsync<InputsResponse>();
			}
        }

        public async Task<IEnumerable<CcjRunningRoundState>> GetStatesAsync()
        {
			using (var response = await _torClient.SendAsync(HttpMethod.Get, "/api/v1/btc/chaumiancoinjoin/states/"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
                {
					string message = await response.Content.ReadAsJsonAsync<string>();
					throw new HttpRequestException(message);
                }
				return await response.Content.ReadAsJsonAsync<IEnumerable<CcjRunningRoundState>>();
			}
        }

        public async Task<string> GetConfirmationAsync(Guid uid, long roundId)
        {
			var queryString = $"/api/v1/btc/chaumiancoinjoin/confirmation?uniqueId={uid}&roundId={roundId}";

			using (var response = await _torClient.SendAsync(HttpMethod.Post, queryString))
			{
				if (!response.IsSuccessStatusCode)
                {
					string message = await response.Content.ReadAsJsonAsync<string>();
					throw new HttpRequestException(message ?? string.Empty);
                }

				if(response.StatusCode == HttpStatusCode.NoContent)
					return string.Empty;

				return await response.Content.ReadAsJsonAsync<string>();
			}
        }

        public void Dispose()
        {
            _torClient.Dispose();
        }
    }
}