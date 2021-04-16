using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class ArenaClient
	{
		public ArenaClient(
			CredentialIssuerParameters amountCredentialIssuerParameters,
			CredentialIssuerParameters vsizeCredentialIssuerParameters,
			CredentialPool amountCredentialPool,
			CredentialPool vsizeCredentialPool,
			IArenaRequestHandler requestHandler,
			WasabiRandom random)
		{
			AmountCredentialClient = new WabiSabiClient(amountCredentialIssuerParameters, random, ProtocolConstants.MaxAmountPerAlice, amountCredentialPool);
			VsizeCredentialClient = new WabiSabiClient(vsizeCredentialIssuerParameters, random, ProtocolConstants.MaxVsizePerAlice, vsizeCredentialPool);
			RequestHandler = requestHandler;
		}

		public ArenaClient(WabiSabiClient amountCredentialClient, WabiSabiClient vsizeCredentialClient, IArenaRequestHandler requestHandler)
		{
			AmountCredentialClient = amountCredentialClient;
			VsizeCredentialClient = vsizeCredentialClient;
			RequestHandler = requestHandler;
		}

		public WabiSabiClient AmountCredentialClient { get; }
		public WabiSabiClient VsizeCredentialClient { get; }
		public IArenaRequestHandler RequestHandler { get; }

		public ValueTask<Guid> RegisterInputAsync(Money amount, OutPoint outPoint, Key key, Guid roundId, uint256 roundHash) =>
			RegisterInputAsync(
				new[] { amount },
				new[] { outPoint },
				new[] { key },
				roundId,
				roundHash);

		public async ValueTask<Guid> RegisterInputAsync(
			IEnumerable<Money> amounts,
			IEnumerable<OutPoint> outPoints,
			IEnumerable<Key> keys,
			Guid roundId,
			uint256 roundHash)
		{
			static byte[] GenerateOwnershipProof(Key key, uint256 roundHash) => OwnershipProof.GenerateCoinJoinInputProof(
				key,
				new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundHash)).ToBytes();

			var registrableInputs = outPoints
				.Zip(keys, (outPoint, key) => (outPoint, key))
				.Select(x => new InputRoundSignaturePair(x.outPoint, GenerateOwnershipProof(x.key, roundHash)));

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroVsizeCredentialRequestData = VsizeCredentialClient.CreateRequestForZeroAmount();

			var inputRegistrationResponse = await RequestHandler.RegisterInputAsync(
				new InputsRegistrationRequest(
					roundId,
					registrableInputs,
					zeroAmountCredentialRequestData.CredentialsRequest,
					zeroVsizeCredentialRequestData.CredentialsRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(inputRegistrationResponse.AmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			VsizeCredentialClient.HandleResponse(inputRegistrationResponse.VsizeCredentials, zeroVsizeCredentialRequestData.CredentialsResponseValidation);

			return inputRegistrationResponse.AliceId;
		}

		public async Task RemoveInputAsync(Guid roundId, Guid aliceId)
		{
			await RequestHandler.RemoveInputAsync(new InputsRemovalRequest(roundId, aliceId)).ConfigureAwait(false);
		}

		public async Task RegisterOutputAsync(Guid roundId, long value, Script scriptPubKey, IEnumerable<Credential> amountCredentialsToPresent, IEnumerable<Credential> vsizeCredentialsToPresent)
		{
			Guard.InRange(nameof(amountCredentialsToPresent), amountCredentialsToPresent, 0, AmountCredentialClient.NumberOfCredentials);
			Guard.InRange(nameof(vsizeCredentialsToPresent), vsizeCredentialsToPresent, 0, VsizeCredentialClient.NumberOfCredentials);

			var presentedAmount = amountCredentialsToPresent.Sum(x => (long)x.Amount.ToUlong());
			var (realAmountCredentialRequest, realAmountCredentialResponseValidation) = AmountCredentialClient.CreateRequest(
				new[] { presentedAmount - value },
				amountCredentialsToPresent);

			var presentedVsize = vsizeCredentialsToPresent.Sum(x => (long)x.Amount.ToUlong());
			var (realVsizeCredentialRequest, realVsizeCredentialResponseValidation) = VsizeCredentialClient.CreateRequest(
				new[] { presentedVsize - scriptPubKey.EstimateOutputVsize() },
				vsizeCredentialsToPresent);

			var outputRegistrationResponse = await RequestHandler.RegisterOutputAsync(
				new OutputRegistrationRequest(
					roundId,
					scriptPubKey,
					realAmountCredentialRequest,
					realVsizeCredentialRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(outputRegistrationResponse.AmountCredentials, realAmountCredentialResponseValidation);
			VsizeCredentialClient.HandleResponse(outputRegistrationResponse.VsizeCredentials, realVsizeCredentialResponseValidation);
		}

		public async Task<bool> ConfirmConnectionAsync(Guid roundId, Guid aliceId, IEnumerable<long> inputsRegistrationVsize, IEnumerable<Credential> amountCredentialsToPresent, IEnumerable<Money> newAmount)
		{
			Guard.InRange(nameof(newAmount), newAmount, 1, AmountCredentialClient.NumberOfCredentials);
			Guard.InRange(nameof(amountCredentialsToPresent), amountCredentialsToPresent, 1, AmountCredentialClient.NumberOfCredentials);
			Guard.InRange(nameof(inputsRegistrationVsize), inputsRegistrationVsize, 1, VsizeCredentialClient.NumberOfCredentials);

			var realAmountCredentialRequestData = AmountCredentialClient.CreateRequest(
				newAmount.Select(x => x.Satoshi),
				amountCredentialsToPresent);

			var realVsizeCredentialRequestData = VsizeCredentialClient.CreateRequest(
				inputsRegistrationVsize,
				Enumerable.Empty<Credential>());

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroVsizeCredentialRequestData = VsizeCredentialClient.CreateRequestForZeroAmount();

			var confirmConnectionResponse = await RequestHandler.ConfirmConnectionAsync(
				new ConnectionConfirmationRequest(
					roundId,
					aliceId,
					zeroAmountCredentialRequestData.CredentialsRequest,
					realAmountCredentialRequestData.CredentialsRequest,
					zeroVsizeCredentialRequestData.CredentialsRequest,
					realVsizeCredentialRequestData.CredentialsRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(confirmConnectionResponse.ZeroAmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			VsizeCredentialClient.HandleResponse(confirmConnectionResponse.ZeroVsizeCredentials, zeroVsizeCredentialRequestData.CredentialsResponseValidation);

			if (confirmConnectionResponse is { RealAmountCredentials: { }, RealVsizeCredentials: { } })
			{
				AmountCredentialClient.HandleResponse(confirmConnectionResponse.RealAmountCredentials, realAmountCredentialRequestData.CredentialsResponseValidation);
				VsizeCredentialClient.HandleResponse(confirmConnectionResponse.RealVsizeCredentials, realVsizeCredentialRequestData.CredentialsResponseValidation);
				return true;
			}

			return false;
		}

		public async Task SignTransactionAsync(Guid roundId, IEnumerable<ICoin> coinsToSign, BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin)
		{
			if (unsignedCoinJoin.Inputs.Count == 0)
			{
				throw new ArgumentException("No inputs to sign.", nameof(unsignedCoinJoin));
			}

			if (!coinsToSign.Any())
			{
				throw new ArgumentException("No coins were provided.", nameof(coinsToSign));
			}

			var myInputs = coinsToSign.ToDictionary(c => c.Outpoint);
			var signedCoinJoin = unsignedCoinJoin.Clone();
			var myInputsFromCoinJoin = signedCoinJoin.Inputs.AsIndexedInputs().Where(input => myInputs.ContainsKey(input.PrevOut)).ToArray();

			if (myInputs.Count != myInputsFromCoinJoin.Length)
			{
				throw new InvalidOperationException($"Missing inputs. Number of inputs: {myInputs.Count} actual: {myInputsFromCoinJoin.Length}.");
			}

			List<InputWitnessPair> signatures = new();
			foreach (var txInput in myInputsFromCoinJoin)
			{
				var coin = myInputs[txInput.PrevOut];

				signedCoinJoin.Sign(bitcoinSecret, coin);

				if (!txInput.VerifyScript(coin, out var error))
				{
					throw new InvalidOperationException($"Witness is missing. Reason {nameof(ScriptError)} code: {error}.");
				}

				signatures.Add(new InputWitnessPair(txInput.Index, txInput.WitScript));
			}

			await RequestHandler.SignTransactionAsync(new TransactionSignaturesRequest(roundId, signatures)).ConfigureAwait(false);
		}
	}
}
