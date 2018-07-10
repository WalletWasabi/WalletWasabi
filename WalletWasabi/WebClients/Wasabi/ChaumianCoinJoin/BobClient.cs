using NBitcoin;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.TorSocks5;
using WalletWasabi.Bases;

namespace WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin
{
	public class BobClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public BobClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		public async Task PostOutputAsync(string roundHash, BitcoinAddress activeOutputAddress, byte[] unblindedSignature)
		{
			var request = new OutputRequest() { OutputAddress = activeOutputAddress.ToString(), SignatureHex = ByteHelpers.ToHex(unblindedSignature) };
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
