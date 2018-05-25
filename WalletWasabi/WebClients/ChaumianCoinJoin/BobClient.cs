using NBitcoin;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Requests;
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

		public async Task PostOutputAsync(string roundHash, BitcoinAddress activeOutputAddress, byte[] unblindedSignature)
		{
			var request = new OutputRequest() { OutputAddress = activeOutputAddress.ToString(), SignatureHex = ByteHelpers.ToHex(unblindedSignature) };
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/output?roundHash={roundHash}", request.ToHttpStringContent()))
			{
				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
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

		#endregion IDisposable Support
	}
}
