using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class SatoshiClient
	{
		public SatoshiClient(IHttpClient httpClient)
		{
			HttpClient = httpClient;
		}

		private IHttpClient HttpClient { get; }

		public async Task<IEnumerable<RoundStateResponseBase>> GetAllRoundStatesAsync()
		{
			using var response = await HttpClient.SendAsync(HttpMethod.Get, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/states/").ConfigureAwait(false);
			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			var states = await response.Content.ReadAsJsonAsync<IEnumerable<RoundStateResponseBase>>().ConfigureAwait(false);
			return states;
		}

		public async Task<RoundStateResponseBase> GetRoundStateAsync(long roundId)
		{
			IEnumerable<RoundStateResponseBase> states = await GetAllRoundStatesAsync().ConfigureAwait(false);
			return states.Single(x => x.RoundId == roundId);
		}

		public async Task<RoundStateResponseBase?> TryGetRegistrableRoundStateAsync()
		{
			IEnumerable<RoundStateResponseBase> states = await GetAllRoundStatesAsync().ConfigureAwait(false);
			return states.FirstOrDefault(x => x.Phase == RoundPhase.InputRegistration);
		}
	}
}
