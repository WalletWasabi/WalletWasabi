using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin
{
	public class BobClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public BobClient(Func<Uri> baseUriAction, EndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
		{
		}

		/// <inheritdoc/>
		public BobClient(Uri baseUri, EndPoint torSocks5EndPoint) : base(baseUri, torSocks5EndPoint)
		{
		}

		/// <returns>If the phase is still in OutputRegistration.</returns>
		public async Task<bool> PostOutputAsync(long roundId, ActiveOutput activeOutput)
		{
			Guard.MinimumAndNotNull(nameof(roundId), roundId, 0);
			Guard.NotNull(nameof(activeOutput), activeOutput);

			var request = new OutputRequest { OutputAddress = activeOutput.Address, UnblindedSignature = activeOutput.Signature, Level = activeOutput.MixingLevel };
			using var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/output?roundId={roundId}", request.ToHttpStringContent());
			if (response.StatusCode == HttpStatusCode.Conflict)
			{
				return false;
			}
			else if (response.StatusCode != HttpStatusCode.NoContent)
			{
				await response.ThrowRequestExceptionFromContentAsync();
			}

			return true;
		}
	}
}
