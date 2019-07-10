using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin
{
	public class AliceClient : TorDisposableBase
	{
		public Guid UniqueId { get; private set; }

		public long RoundId { get; }
		public Network Network { get; }

		public BitcoinAddress[] RegisteredAddresses { get; }
		public SchnorrPubKey[] SchnorrPubKeys { get; }
		public Requester[] Requesters { get; }

		/// <inheritdoc/>
		private AliceClient(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			Func<Uri> baseUriAction,
			IPEndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
		{
			RoundId = roundId;
			RegisteredAddresses = registeredAddresses.ToArray();
			SchnorrPubKeys = schnorrPubKeys.ToArray();
			Requesters = requesters.ToArray();
			Network = network;
		}

		public static async Task<AliceClient> CreateNewAsync(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			InputsRequest request,
			Uri baseUri,
			IPEndPoint torSocks5EndPoint)
		{
			return await CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, request, () => baseUri, torSocks5EndPoint);
		}

		public static async Task<AliceClient> CreateNewAsync(long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			InputsRequest request,
			Func<Uri> baseUriAction,
			IPEndPoint torSocks5EndPoint)
		{
			AliceClient client = new AliceClient(roundId, registeredAddresses, schnorrPubKeys, requesters, network, baseUriAction, torSocks5EndPoint);
			try
			{
				// Correct it if forgot to set.
				if (request.RoundId != roundId)
				{
					if (request.RoundId == 0)
					{
						request.RoundId = roundId;
					}
					else
					{
						throw new NotSupportedException($"InputRequest roundId does not match to the provided roundId: {request.RoundId} != {roundId}.");
					}
				}
				using (HttpResponseMessage response = await client.TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/inputs/", request.ToHttpStringContent()))
				{
					if (response.StatusCode != HttpStatusCode.OK)
					{
						await response.ThrowRequestExceptionFromContentAsync();
					}

					var inputsResponse = await response.Content.ReadAsJsonAsync<InputsResponse>();

					if (inputsResponse.RoundId != roundId) // This should never happen. If it does, that's a bug in the coordinator.
					{
						throw new NotSupportedException($"Coordinator assigned us to the wrong round: {inputsResponse.RoundId}. Requested round: {roundId}.");
					}

					client.UniqueId = inputsResponse.UniqueId;
					Logger.LogInfo<AliceClient>($"Round ({client.RoundId}), Alice ({client.UniqueId}): Registered {request.Inputs.Count()} inputs.");

					return client;
				}
			}
			catch
			{
				client?.Dispose();
				throw;
			}
		}

		public static async Task<AliceClient> CreateNewAsync(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			BitcoinAddress changeOutput,
			IEnumerable<uint256> blindedOutputScriptHashes,
			IEnumerable<InputProofModel> inputs,
			Uri baseUri,
			IPEndPoint torSocks5EndPoint)
		{
			return await CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, changeOutput, blindedOutputScriptHashes, inputs, () => baseUri, torSocks5EndPoint);
		}

		public static async Task<AliceClient> CreateNewAsync(long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			BitcoinAddress changeOutput,
			IEnumerable<uint256> blindedOutputScriptHashes,
			IEnumerable<InputProofModel> inputs,
			Func<Uri> baseUriAction,
			IPEndPoint torSocks5EndPoint)
		{
			var request = new InputsRequest {
				RoundId = roundId,
				BlindedOutputScripts = blindedOutputScriptHashes,
				ChangeOutputAddress = changeOutput,
				Inputs = inputs
			};
			return await CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, request, baseUriAction, torSocks5EndPoint);
		}

		public async Task<(CcjRoundPhase currentPhase, IEnumerable<ActiveOutput> activeOutputs)> PostConfirmationAsync()
		{
			using (HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={UniqueId}&roundId={RoundId}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				ConnConfResp resp = await response.Content.ReadAsJsonAsync<ConnConfResp>();
				Logger.LogInfo<AliceClient>($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection. Phase: {resp.CurrentPhase}.");

				var activeOutputs = new List<ActiveOutput>();
				if (resp.BlindedOutputSignatures != null && resp.BlindedOutputSignatures.Any())
				{
					var unblindedSignatures = new List<UnblindedSignature>();
					var blindedSignatures = resp.BlindedOutputSignatures.ToArray();
					for (int i = 0; i < blindedSignatures.Length; i++)
					{
						uint256 blindedSignature = blindedSignatures[i];
						Requester requester = Requesters[i];
						UnblindedSignature unblindedSignature = requester.UnblindSignature(blindedSignature);

						var address = RegisteredAddresses[i];

						uint256 outputScriptHash = new uint256(Hashes.SHA256(address.ScriptPubKey.ToBytes()));
						PubKey signerPubKey = SchnorrPubKeys[i].SignerPubKey;
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
		}

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
				catch (Exception ex) when (ex is OperationCanceledException // If could not do it within 3 seconds then it will likely time out and take it as unconfirmed.
										|| ex is TaskCanceledException
										|| ex is TimeoutException)
				{
					return;
				}
				catch (ConnectionException)  // If some internet connection issue then it will likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (TorSocks5FailureResponseException) // If some Tor connection issue then it will likely time out and take it as unconfirmed.
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
