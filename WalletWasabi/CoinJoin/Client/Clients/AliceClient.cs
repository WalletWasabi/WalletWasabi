using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.CoinJoin.Common.Crypto;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Exceptions;
using WalletWasabi.Logging;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Client.Clients
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
			EndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
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
			EndPoint torSocks5EndPoint)
		{
			return await CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, request, () => baseUri, torSocks5EndPoint).ConfigureAwait(false);
		}

		public static async Task<AliceClient> CreateNewAsync(long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			InputsRequest request,
			Func<Uri> baseUriAction,
			EndPoint torSocks5EndPoint)
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
						throw new NotSupportedException($"InputRequest {nameof(roundId)} does not match to the provided {nameof(roundId)}: {request.RoundId} != {roundId}.");
					}
				}
				using HttpResponseMessage response = await client.TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/inputs/", request.ToHttpStringContent()).ConfigureAwait(false);
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
			EndPoint torSocks5EndPoint)
		{
			return await CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, changeOutput, blindedOutputScriptHashes, inputs, () => baseUri, torSocks5EndPoint).ConfigureAwait(false);
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
			EndPoint torSocks5EndPoint)
		{
			var request = new InputsRequest
			{
				RoundId = roundId,
				BlindedOutputScripts = blindedOutputScriptHashes,
				ChangeOutputAddress = changeOutput,
				Inputs = inputs
			};
			return await CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, request, baseUriAction, torSocks5EndPoint).ConfigureAwait(false);
		}

		public async Task<(RoundPhase currentPhase, IEnumerable<ActiveOutput> activeOutputs)> PostConfirmationAsync()
		{
			using HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={UniqueId}&roundId={RoundId}").ConfigureAwait(false);
			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			ConnectionConfirmationResponse resp = await response.Content.ReadAsJsonAsync<ConnectionConfirmationResponse>().ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Confirmed connection. Phase: {resp.CurrentPhase}.");

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

		public async Task PostUnConfirmationAsync()
		{
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
			{
				try
				{
					using HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/unconfirmation?uniqueId={UniqueId}&roundId={RoundId}", cancel: cts.Token).ConfigureAwait(false);
					if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Gone) // Otherwise maybe some internet connection issue there's. Let's consider that as timed out.
					{
						await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
					}
				}
				catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException) // If could not do it within 3 seconds then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (ConnectionException) // If some internet connection issue then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
				catch (TorSocks5FailureResponseException) // If some Tor connection issue then it'll likely time out and take it as unconfirmed.
				{
					return;
				}
			}
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Unconfirmed connection.");
		}

		public async Task<Transaction> GetUnsignedCoinJoinAsync()
		{
			using HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Get, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/coinjoin?uniqueId={UniqueId}&roundId={RoundId}").ConfigureAwait(false);
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
			var signatureRequestContent = new StringContent(jsonSignatures, Encoding.UTF8, "application/json");

			using HttpResponseMessage response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/signatures?uniqueId={UniqueId}&roundId={RoundId}", signatureRequestContent).ConfigureAwait(false);
			if (response.StatusCode != HttpStatusCode.NoContent)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}
			Logger.LogInfo($"Round ({RoundId}), Alice ({UniqueId}): Posted {signatures.Count} signatures.");
		}
	}
}
