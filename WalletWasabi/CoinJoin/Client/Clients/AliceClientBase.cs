using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.Crypto.SchnorrBlinding;
using UnblindedSignature = WalletWasabi.Crypto.UnblindedSignature;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public abstract class AliceClientBase
	{
		protected AliceClientBase(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<Requester> requesters,
			Network network,
			IHttpClient httpClient)
		{
			HttpClient = httpClient;
			RoundId = roundId;
			RegisteredAddresses = registeredAddresses.ToArray();
			Requesters = requesters.ToArray();
			Network = network;
		}

		public Guid UniqueId { get; private set; }

		public long RoundId { get; }
		public Network Network { get; }

		public BitcoinAddress[] RegisteredAddresses { get; }
		public Requester[] Requesters { get; }

		public IHttpClient HttpClient { get; }

		public static async Task<AliceClient4> CreateNewAsync(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<PubKey> signerPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			BitcoinAddress changeOutput,
			IEnumerable<BlindedOutputWithNonceIndex> blindedOutputScriptHashes,
			IEnumerable<InputProofModel> inputs,
			IHttpClient httpClient)
		{
			var request = new InputsRequest4
			{
				RoundId = roundId,
				BlindedOutputScripts = blindedOutputScriptHashes,
				ChangeOutputAddress = changeOutput,
				Inputs = inputs
			};
			AliceClient4 client = new(roundId, registeredAddresses, signerPubKeys, requesters, network, httpClient);

			// Correct it if forgot to set.
			if (request.RoundId != roundId)
			{
				if (request.RoundId == 0)
				{
					request.RoundId = roundId;
				}
				else
				{
					throw new NotSupportedException($"InputRequest {nameof(roundId)} does not match to the provided {nameof(roundId)}: {request.RoundId} != {roundId}.");
				}
			}

			using StringContent content = request.ToHttpStringContent();
			using HttpResponseMessage response = await client.HttpClient.SendAsync(HttpMethod.Post, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/inputs/", content).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			var inputsResponse = await response.Content.ReadAsJsonAsync<InputsResponse>().ConfigureAwait(false);

			if (inputsResponse.RoundId != roundId) // This should never happen. If it does, that's a bug in the coordinator.
			{
				throw new NotSupportedException($"Coordinator assigned us to the wrong round: {inputsResponse.RoundId}. Requested round: {roundId}.");
			}

			client.UniqueId = inputsResponse.UniqueId;
			Logger.LogInfo($"Round ({client.RoundId}), Alice ({client.UniqueId}): Registered {request.Inputs.Count()} inputs.");

			return client;
		}

		public async Task<(RoundPhase currentPhase, IEnumerable<ActiveOutput> activeOutputs)> PostConfirmationAsync()
		{
			using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Post, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={UniqueId}&roundId={RoundId}").ConfigureAwait(false);
			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			ConnectionConfirmationResponse resp = await response.Content.ReadAsJsonAsync<ConnectionConfirmationResponse>().ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection. Phase: {resp.CurrentPhase}.");

			var activeOutputs = new List<ActiveOutput>();
			if (resp.BlindedOutputSignatures is { } && resp.BlindedOutputSignatures.Any())
			{
				var unblindedSignatures = new List<UnblindedSignature>();
				var blindedSignatures = resp.BlindedOutputSignatures.ToArray();
				for (int i = 0; i < blindedSignatures.Length; i++)
				{
					uint256 blindedSignature = blindedSignatures[i];
					Requester requester = Requesters[i];
					UnblindedSignature unblindedSignature = requester.UnblindSignature(blindedSignature);

					var address = RegisteredAddresses[i];

					uint256 outputScriptHash = new(NBitcoin.Crypto.Hashes.SHA256(address.ScriptPubKey.ToBytes()));
					PubKey signerPubKey = GetSignerPubKey(i);
					if (!VerifySignature(outputScriptHash, unblindedSignature, signerPubKey))
					{
						throw new NotSupportedException($"Coordinator did not sign the blinded output properly for level: {i}.");
					}

					unblindedSignatures.Add(unblindedSignature);
				}

				for (int i = 0; i < Math.Min(unblindedSignatures.Count, RegisteredAddresses.Length); i++)
				{
					var sig = unblindedSignatures[i];
					var addr = RegisteredAddresses[i];
					var lvl = i;

					var actOut = new ActiveOutput(addr, sig, lvl);
					activeOutputs.Add(actOut);
				}
			}

			return (resp.CurrentPhase, activeOutputs);
		}

		protected abstract PubKey GetSignerPubKey(int i);

		public async Task PostUnConfirmationAsync()
		{
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
			{
				try
				{
					using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Post, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/unconfirmation?uniqueId={UniqueId}&roundId={RoundId}", cancel: cts.Token).ConfigureAwait(false);
					if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Gone) // Otherwise maybe some internet connection issue there's. Let's consider that as timed out.
					{
						await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
					}
				}
				catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) // If could not do it within 3 seconds then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (HttpRequestException ex) when (ex.InnerException is TorException) // If some Tor connection issue then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
			}
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Unconfirmed connection.");
		}

		public async Task<Transaction> GetUnsignedCoinJoinAsync()
		{
			using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Get, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/coinjoin?uniqueId={UniqueId}&roundId={RoundId}").ConfigureAwait(false);
			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			var coinjoinHex = await response.Content.ReadAsJsonAsync<string>().ConfigureAwait(false);

			Transaction coinJoin = Transaction.Parse(coinjoinHex, Network.Main);
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Acquired unsigned CoinJoin: {coinJoin.GetHash()}.");
			return coinJoin;
		}

		public async Task PostSignaturesAsync(IDictionary<int, WitScript> signatures)
		{
			var myDic = signatures.ToDictionary(signature => signature.Key, signature => signature.Value.ToString());

			var jsonSignatures = JsonConvert.SerializeObject(myDic, Formatting.None);
			using StringContent signatureRequestContent = new(jsonSignatures, Encoding.UTF8, "application/json");

			using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Post, $"/api/v{WasabiClient.ApiVersion}/btc/chaumiancoinjoin/signatures?uniqueId={UniqueId}&roundId={RoundId}", signatureRequestContent).ConfigureAwait(false);
			if (response.StatusCode != HttpStatusCode.NoContent)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Posted {signatures.Count} signatures.");
		}
	}
}
