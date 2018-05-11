using NBitcoin;
using Newtonsoft.Json;
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
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class AliceClient : IDisposable
	{
		public TorHttpClient TorClient { get; }

		public long RoundId { get; private set; }
		public Guid UniqueId { get; private set; }
		public byte[] BlindedOutputSignature { get; private set; }

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		private AliceClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			TorClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
		}

		public static async Task<AliceClient> CreateNewAsync(InputsRequest request, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			AliceClient client = new AliceClient(baseUri, torSocks5EndPoint);
			try
			{
				using (HttpResponseMessage response = await client.TorClient.SendAsync(HttpMethod.Post, "/api/v1/btc/chaumiancoinjoin/inputs/", request.ToHttpStringContent()))
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

					var inputsResponse = await response.Content.ReadAsJsonAsync<InputsResponse>();

					client.RoundId = inputsResponse.RoundId;
					client.UniqueId = inputsResponse.UniqueId;
					client.BlindedOutputSignature = inputsResponse.BlindedOutputSignature;

					return client;
				}
			}
			catch
			{
				client.Dispose();
				throw;
			}
		}

		public static async Task<AliceClient> CreateNewAsync(Script changeOutput, byte[] blindedData, IEnumerable<InputProofModel> inputs, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			var request =  new InputsRequest
			{
				BlindedOutputScriptHex = ByteHelpers.ToHex(blindedData),
				ChangeOutputScript = changeOutput.ToString(),
				Inputs = inputs
			};
			return await CreateNewAsync(request, baseUri, torSocks5EndPoint);
		}

		/// <returns>null or roundHash</returns>
		public async Task<string> PostConfirmationAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/confirmation?uniqueId={UniqueId}&roundId={RoundId}"))
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
				{
					return null;
				}
				else if (response.StatusCode == HttpStatusCode.OK)
				{
					return await response.Content.ReadAsJsonAsync<string>();
				}
				else
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

		/// <returns>null or roundHash</returns>
		public async Task PostUnConfirmationAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/unconfirmation?uniqueId={UniqueId}&roundId={RoundId}"))
			{
				if (!response.IsSuccessStatusCode)
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

		public async Task<Transaction> GetUnsignedCoinJoinAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v1/btc/chaumiancoinjoin/coinjoin?uniqueId={UniqueId}&roundId={RoundId}"))
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

				var coinjoinHex = await response.Content.ReadAsJsonAsync<string>();
				return new Transaction(coinjoinHex);
			}
		}

		public async Task PostSignaturesAsync(IDictionary<int, WitScript> signatures)
		{
			var myDic = new Dictionary<int, string>();
			foreach(var signature in signatures)
			{
				myDic.Add(signature.Key, signature.Value.ToString());
			}

			var jsonSignatures = JsonConvert.SerializeObject(myDic, Formatting.None);
			var signatureRequestContent = new StringContent(jsonSignatures, Encoding.UTF8, "application/json");

			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/signatures?uniqueId={UniqueId}&roundId={RoundId}", signatureRequestContent))
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
