using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin
{
	public class BobClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public BobClient(Func<Uri> baseUriAction, IPEndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
		{
		}

		/// <inheritdoc/>
		public BobClient(Uri baseUri, IPEndPoint torSocks5EndPoint) : base(baseUri, torSocks5EndPoint)
		{
		}

		/// <returns>If the phase is still in OutputRegistration.</returns>
		public async Task<bool> PostOutputAsync(long roundId, BitcoinAddress activeOutputAddress, UnblindedSignature unblindedSignature, int level)
		{
			Guard.MinimumAndNotNull(nameof(roundId), roundId, 0);
			Guard.NotNull(nameof(activeOutputAddress), activeOutputAddress);
			Guard.NotNull(nameof(unblindedSignature), unblindedSignature);
			Guard.MinimumAndNotNull(nameof(level), level, 0);

			var request = new OutputRequest { OutputAddress = activeOutputAddress, UnblindedSignature = unblindedSignature, Level = level };
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/output?roundId={roundId}", request.ToHttpStringContent()).ConfigureAwait(false))
			{
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
}
