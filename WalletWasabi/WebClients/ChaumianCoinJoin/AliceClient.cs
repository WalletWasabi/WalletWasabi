using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class AliceClient : IDisposable
	{
		public TorHttpClient TorClient { get; }

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		public AliceClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			TorClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
		}

		public async Task<InputsResponse> PostInputsAsync(InputsRequest request)
		{
			using (var response = await TorClient.SendAsync(HttpMethod.Post, "/api/v1/btc/chaumiancoinjoin/inputs/", request.ToHttpStringContent()))
			{
				if (response.StatusCode != HttpStatusCode.OK)
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

				return await response.Content.ReadAsJsonAsync<InputsResponse>();
			}
		}

		public async Task<InputsResponse> PostInputsAsync(Script changeOutput, byte[] blindedData, params InputProofModel[] inputs)
		{
			var request =  new InputsRequest
			{
				BlindedOutputScriptHex = ByteHelpers.ToHex(blindedData),
				ChangeOutputScript = changeOutput.ToString(),
				Inputs = inputs
			};
			return await PostInputsAsync(request);
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

		// ~AliceClient() {
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
