using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.CoinJoin.Common.Models;

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

		public async Task<IEnumerable<RoundStateResponse>> GetAllRoundStatesAsync()
		{
			using var response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/states/");
			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync();
			}

			var states = await response.Content.ReadAsJsonAsync<IEnumerable<RoundStateResponse>>();

			return states;
		}

		public async Task<RoundStateResponse> GetRoundStateAsync(long roundId)
		{
			IEnumerable<RoundStateResponse> states = await GetAllRoundStatesAsync();
			return states.Single(x => x.RoundId == roundId);
		}

		public async Task<RoundStateResponse> GetRegistrableRoundStateAsync()
		{
			IEnumerable<RoundStateResponse> states = await GetAllRoundStatesAsync();
			return states.First(x => x.Phase == RoundPhase.InputRegistration);
		}
	}
}
