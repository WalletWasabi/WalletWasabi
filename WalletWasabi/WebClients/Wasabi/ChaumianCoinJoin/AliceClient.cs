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
using WalletWasabi.Bases;
using System.Threading;
using WalletWasabi.Exceptions;

namespace WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin
{
	public class AliceClient : TorDisposableBase
	{
		public long RoundId { get; private set; }
		public Guid UniqueId { get; private set; }
		public byte[] BlindedOutputSignature { get; private set; }
		public Network Network { get; }

		/// <inheritdoc/>
		private AliceClient(Network network, Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
			Network = network;
		}

		public static async Task<AliceClient> CreateNewAsync(Network network, InputsRequest request, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			AliceClient client = new AliceClient(network, baseUri, torSocks5EndPoint);
			try
			{
				using (HttpResponseMessage response = await client.TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/inputs/", request.ToHttpStringContent()))
				{
					if (response.StatusCode != HttpStatusCode.OK)
					{
						await response.ThrowRequestExceptionFromContentAsync();
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

		public static async Task<AliceClient> CreateNewAsync(Network network, BitcoinAddress changeOutput, uint256 blindedData, IEnumerable<InputProofModel> inputs, Uri baseUri, IPEndPoint torSocks5EndPoint = null)
		{
			var request = new InputsRequest
			{
				BlindedOutputScriptHex = ByteHelpers.ToHex(blindedData.ToBytes()),
				ChangeOutputAddress = changeOutput.ToString(),
				Inputs = inputs
			};
			return await CreateNewAsync(network, request, baseUri, torSocks5EndPoint);
		}

		/// <returns>null or roundHash</returns>
		public async Task<string> PostConfirmationAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={UniqueId}&roundId={RoundId}"))
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
				{
					Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection.");
					return null;
				}

				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				string roundHash = await response.Content.ReadAsJsonAsync<string>();
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection. Acquired roundHash: {roundHash}.");
				return roundHash;
			}
		}

		/// <returns>null or roundHash</returns>
		public async Task PostUnConfirmationAsync()
		{
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
			{
				try
				{
					using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/unconfirmation?uniqueId={UniqueId}&roundId={RoundId}", cancel: cts.Token))
					{
						if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Gone) // Otherwise maybe some internet connection issue there's. Let's consider that as timed out.
						{
							await response.ThrowRequestExceptionFromContentAsync();
						}
					}
				}
				catch (TaskCanceledException) // If couldn't do it within 3 seconds then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (OperationCanceledException) // If couldn't do it within 3 seconds then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (TimeoutException) // If couldn't do it within 3 seconds then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (ConnectionException)  // If some internet connection issue then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (TorSocks5FailureResponseException) // If some Tor connection issue then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
			}
			Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Unconfirmed connection.");
		}

		public async Task<Transaction> GetUnsignedCoinJoinAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/coinjoin?uniqueId={UniqueId}&roundId={RoundId}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				var coinjoinHex = await response.Content.ReadAsJsonAsync<string>();

				Transaction coinJoin = Transaction.Parse(coinjoinHex, Network.Main);
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Acquired unsigned CoinJoin: {coinJoin.GetHash()}.");
				return coinJoin;
			}
		}

		public async Task PostSignaturesAsync(IDictionary<int, WitScript> signatures)
		{
			var myDic = signatures.ToDictionary(signature => signature.Key, signature => signature.Value.ToString());

			var jsonSignatures = JsonConvert.SerializeObject(myDic, Formatting.None);
			var signatureRequestContent = new StringContent(jsonSignatures, Encoding.UTF8, "application/json");

			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/signatures?uniqueId={UniqueId}&roundId={RoundId}", signatureRequestContent))
			{
				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Posted {signatures.Count} signatures.");
			}
		}
	}
}
