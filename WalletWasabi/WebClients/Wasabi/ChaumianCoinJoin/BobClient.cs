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
		public BobClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		public async Task PostOutputAsync(string roundHash, BitcoinAddress activeOutputAddress, BlindSignature unblindedSignature)
		{
			var request = new OutputRequest { OutputAddress = activeOutputAddress.ToString(), Signature = unblindedSignature };
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/output?roundHash={roundHash}", request.ToHttpStringContent()))
			{
				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}
			}
		}
	}
}
