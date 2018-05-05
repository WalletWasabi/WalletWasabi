using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
    public class BobClient : IDisposable
    {
		public TorHttpClient TorClient { get; }

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		public BobClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			TorClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
		}

		public async Task PostOutputAsync(string roundHash, Script activeOutput, byte[] unblindedSignature)
		{
			var outputRequest = new OutputRequest() { OutputScript = activeOutput.ToString(), SignatureHex = ByteHelpers.ToHex(unblindedSignature) };
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/output?roundHash={roundHash}", outputRequest.ToHttpStringContent()))
			{
				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					if (error == null)
					{
						throw new HttpRequestException(response.StatusCode.ToReasonString());
					}
					else
					{
						throw new HttpRequestException($"{response.StatusCode.ToReasonString()}\n{error}");
					}
				}
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					TorClient?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// ~BobClient() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
