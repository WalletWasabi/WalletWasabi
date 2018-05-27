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
using WalletWasabi.Logging;
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
						var errorMessage = error == null ? string.Empty : $"\n{error}";
						throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
					}

					var inputsResponse = await response.Content.ReadAsJsonAsync<InputsResponse>();

					client.RoundId = inputsResponse.RoundId;
					client.UniqueId = inputsResponse.UniqueId;
					client.BlindedOutputSignature = inputsResponse.BlindedOutputSignature;
					Logger.LogInfo<AliceClient>($"Round ({client.RoundId}), Alice ({client.UniqueId}): Registered {request.Inputs.Count()} inputs.");

					return client;
				}
			}
			catch
			{
				client.Dispose();
				throw;
			}
		}

		public static async Task<AliceClient> CreateNewAsync(BitcoinAddress changeOutput, byte[] blindedData, IEnumerable<InputProofModel> inputs, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			var request = new InputsRequest
			{
				BlindedOutputScriptHex = ByteHelpers.ToHex(blindedData),
				ChangeOutputAddress = changeOutput.ToString(),
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
					Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection.");
					return null;
				}
				
				if (response.StatusCode == HttpStatusCode.OK)
				{
					string roundHash = await response.Content.ReadAsJsonAsync<string>();
					Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection. Acquired roundHash: {roundHash}.");
					return roundHash;
				}

				string error = await response.Content.ReadAsJsonAsync<string>();
				var errorMessage = error == null ? string.Empty : $"\n{error}";
				throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
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
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Unconfirmed connection.");
			}
		}

		public async Task<Transaction> GetUnsignedCoinJoinAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v1/btc/chaumiancoinjoin/coinjoin?uniqueId={UniqueId}&roundId={RoundId}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}

				var coinjoinHex = await response.Content.ReadAsJsonAsync<string>();
				Transaction coinJoin = new Transaction(coinjoinHex);
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Acquired unsigned CoinJoin: {coinJoin.GetHash()}.");
				return coinJoin;
			}
		}

		public async Task PostSignaturesAsync(IDictionary<int, WitScript> signatures)
		{
			var myDic = signatures.ToDictionary(signature => signature.Key, signature => signature.Value.ToString());

			var jsonSignatures = JsonConvert.SerializeObject(myDic, Formatting.None);
			var signatureRequestContent = new StringContent(jsonSignatures, Encoding.UTF8, "application/json");

			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/signatures?uniqueId={UniqueId}&roundId={RoundId}", signatureRequestContent))
			{
				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Posted {signatures.Count} signatures.");
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

		#endregion IDisposable Support
	}
}
