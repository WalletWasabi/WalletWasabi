using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class SatoshiClient : TorDisposableSupport
	{

		public SatoshiClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{

		}

		public async Task<IEnumerable<CcjRunningRoundState>> GetAllRoundStatesAsync()
		{
			using (var response = await TorClient.SendAsync(HttpMethod.Get, "/api/v1/btc/chaumiancoinjoin/states/"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
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
