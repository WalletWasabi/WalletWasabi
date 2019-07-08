using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin
{
	public class SatoshiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public SatoshiClient(Func<Uri> baseUriAction, IPEndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
		{
		}

		/// <inheritdoc/>
		public SatoshiClient(Uri baseUri, IPEndPoint torSocks5EndPoint) : base(baseUri, torSocks5EndPoint)
		{
		}

		public async Task<IEnumerable<CcjRunningRoundState>> GetAllRoundStatesAsync()
		{
			using (var response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/states/"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				var states = await response.Content.ReadAsJsonAsync<IEnumerable<CcjRunningRoundState>>();

				return states;
			}
		}

		public async Task<CcjRunningRoundState> GetRoundStateAsync(long roundId)
		{
			IEnumerable<CcjRunningRoundState> states = await GetAllRoundStatesAsync();
			return states.Single(x => x.RoundId == roundId);
		}

		public async Task<CcjRunningRoundState> GetRegistrableRoundStateAsync()
		{
			IEnumerable<CcjRunningRoundState> states = await GetAllRoundStatesAsync();
			return states.First(x => x.Phase == CcjRoundPhase.InputRegistration);
		}
	}
}
