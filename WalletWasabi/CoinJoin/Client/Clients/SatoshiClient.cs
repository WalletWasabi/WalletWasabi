using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class SatoshiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public SatoshiClient(Func<Uri> baseUriAction, EndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
		{
		}

		/// <inheritdoc/>
		public SatoshiClient(Uri baseUri, EndPoint torSocks5EndPoint) : base(baseUri, torSocks5EndPoint)
		{
		}

		public async Task<IEnumerable<RoundStateResponseBase>> GetAllRoundStatesAsync()
		{
			using var response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/states/").ConfigureAwait(false);
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

		public async Task<RoundStateResponseBase> GetRegistrableRoundStateAsync()
		{
			IEnumerable<RoundStateResponseBase> states = await GetAllRoundStatesAsync().ConfigureAwait(false);
			return states.First(x => x.Phase == RoundPhase.InputRegistration);
		}
	}
}
