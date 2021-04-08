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
		public static readonly int ProtocolCredentialNumber = 2;
		public static readonly ulong ProtocolMaxAmountPerAlice = 4_300_000_000_000ul;
		public static readonly ulong ProtocolMaxWeightPerAlice = 1_000ul;

		public ArenaClient(
			CredentialIssuerParameters amountCredentialIssuerParameters,
			CredentialIssuerParameters weightCredentialIssuerParameters,
			IArenaRequestHandler requestHandler,
			WasabiRandom random)
		{
			var amountCredentials = new CredentialPool();
			var weightCredentials = new CredentialPool();

			AmountCredentialClient = new WabiSabiClient(amountCredentialIssuerParameters, ProtocolCredentialNumber, random, ProtocolMaxAmountPerAlice, amountCredentials);
			WeightCredentialClient = new WabiSabiClient(weightCredentialIssuerParameters, ProtocolCredentialNumber, random, ProtocolMaxWeightPerAlice, weightCredentials);
			RequestHandler = requestHandler;
		}

		public ArenaClient(WabiSabiClient amountCredentialClient, WabiSabiClient weightCredentialClient, IArenaRequestHandler requestHandler)
		{
			AmountCredentialClient = amountCredentialClient;
			WeightCredentialClient = weightCredentialClient;
			RequestHandler = requestHandler;
		}

		public WabiSabiClient AmountCredentialClient { get; }
		public WabiSabiClient WeightCredentialClient { get; }
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
			var zeroWeightCredentialRequestData = WeightCredentialClient.CreateRequestForZeroAmount();

			var inputRegistrationResponse = await RequestHandler.RegisterInputAsync(
				new InputsRegistrationRequest(
					roundId,
					registrableInputs,
					zeroAmountCredentialRequestData.CredentialsRequest,
					zeroWeightCredentialRequestData.CredentialsRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(inputRegistrationResponse.AmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			WeightCredentialClient.HandleResponse(inputRegistrationResponse.WeightCredentials, zeroWeightCredentialRequestData.CredentialsResponseValidation);

			return inputRegistrationResponse.AliceId;
		}

		public async Task RemoveInputAsync(Guid roundId, Guid aliceId)
		{
			await RequestHandler.RemoveInputAsync(new InputsRemovalRequest(roundId, aliceId)).ConfigureAwait(false);
		}

		public async Task RegisterOutputAsync(Guid roundId, long value, Script scriptPubKey, IEnumerable<Credential> amountCredentialsToPresent, IEnumerable<Credential> weightCredentialsToPresent)
		{
			Guard.InRange(nameof(amountCredentialsToPresent), amountCredentialsToPresent, 0, AmountCredentialClient.NumberOfCredentials);
			Guard.InRange(nameof(weightCredentialsToPresent), weightCredentialsToPresent, 0, WeightCredentialClient.NumberOfCredentials);

			var presentedAmount = amountCredentialsToPresent.Sum(x => (long)x.Amount.ToUlong());
			var (realAmountCredentialRequest, realAmountCredentialResponseValidation) = AmountCredentialClient.CreateRequest(
				new[] { presentedAmount - value },
				amountCredentialsToPresent);

			var presentedWeight = weightCredentialsToPresent.Sum(x => (long)x.Amount.ToUlong());
			var (realWeightCredentialRequest, realWeightCredentialResponseValidation) = WeightCredentialClient.CreateRequest(
				new[] { presentedWeight - Constants.WitnessScaleFactor * scriptPubKey.EstimateOutputVsize() },
				weightCredentialsToPresent);

			var outputRegistrationResponse = await RequestHandler.RegisterOutputAsync(
				new OutputRegistrationRequest(
					roundId,
					scriptPubKey,
					realAmountCredentialRequest,
					realWeightCredentialRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(outputRegistrationResponse.AmountCredentials, realAmountCredentialResponseValidation);
			WeightCredentialClient.HandleResponse(outputRegistrationResponse.WeightCredentials, realWeightCredentialResponseValidation);
		}

		public async Task<bool> ConfirmConnectionAsync(Guid roundId, Guid aliceId, IEnumerable<long> inputsRegistrationWeight, IEnumerable<Credential> amountCredentialsToPresent, IEnumerable<Money> newAmount)
		{
			Guard.InRange(nameof(newAmount), newAmount, 1, AmountCredentialClient.NumberOfCredentials);
			Guard.InRange(nameof(amountCredentialsToPresent), amountCredentialsToPresent, 1, AmountCredentialClient.NumberOfCredentials);
			Guard.InRange(nameof(inputsRegistrationWeight), inputsRegistrationWeight, 1, WeightCredentialClient.NumberOfCredentials);

			var realAmountCredentialRequestData = AmountCredentialClient.CreateRequest(
				newAmount.Select(x => x.Satoshi),
				amountCredentialsToPresent);

			var realWeightCredentialRequestData = WeightCredentialClient.CreateRequest(
				inputsRegistrationWeight,
				Enumerable.Empty<Credential>());

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroWeightCredentialRequestData = WeightCredentialClient.CreateRequestForZeroAmount();

			var confirmConnectionResponse = await RequestHandler.ConfirmConnectionAsync(
				new ConnectionConfirmationRequest(
					roundId,
					aliceId,
					zeroAmountCredentialRequestData.CredentialsRequest,
					realAmountCredentialRequestData.CredentialsRequest,
					zeroWeightCredentialRequestData.CredentialsRequest,
					realWeightCredentialRequestData.CredentialsRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(confirmConnectionResponse.ZeroAmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			WeightCredentialClient.HandleResponse(confirmConnectionResponse.ZeroWeightCredentials, zeroWeightCredentialRequestData.CredentialsResponseValidation);
			if (confirmConnectionResponse is { RealAmountCredentials: { }, RealWeightCredentials: { } })
			{
				AmountCredentialClient.HandleResponse(confirmConnectionResponse.RealAmountCredentials, realAmountCredentialRequestData.CredentialsResponseValidation);
				WeightCredentialClient.HandleResponse(confirmConnectionResponse.RealWeightCredentials, realWeightCredentialRequestData.CredentialsResponseValidation);
				return true;
			}
			return false
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
