using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class BobClient
	{
		public BobClient(IHttpClient httpClient)
		{
			HttpClient = httpClient;
		}

		private IHttpClient HttpClient { get; }

		/// <returns>If the phase is still in OutputRegistration.</returns>
		public async Task<bool> PostOutputAsync(long roundId, ActiveOutput activeOutput)
		{
			Guard.MinimumAndNotNull(nameof(roundId), roundId, 0);
			Guard.NotNull(nameof(activeOutput), activeOutput);

			var request = new OutputRequest { OutputAddress = activeOutput.Address, UnblindedSignature = activeOutput.Signature, Level = activeOutput.MixingLevel };
			using var requestStringContent = request.ToHttpStringContent();
			using var response = await HttpClient.SendAsync(HttpMethod.Post, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/output?roundId={roundId}", requestStringContent).ConfigureAwait(false);
			if (response.StatusCode == HttpStatusCode.Conflict)
			{
				return false;
			}
			else if (response.StatusCode != HttpStatusCode.NoContent)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			return true;
		}
	}
}
