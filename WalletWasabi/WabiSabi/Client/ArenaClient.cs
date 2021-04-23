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

		public async ValueTask<Guid> RegisterInputAsync(
			Money amount,
			OutPoint outPoint,
			Key key,
			Guid roundId,
			uint256 roundHash)
		{
			var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
				key,
				new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundHash)).ToBytes();

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroVsizeCredentialRequestData = VsizeCredentialClient.CreateRequestForZeroAmount();

			var inputRegistrationResponse = await RequestHandler.RegisterInputAsync(
				new InputRegistrationRequest(
					roundId,
					outPoint,
					ownershipProof,
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

		public async Task<(IEnumerable<Credential> RealAmountCredentials, IEnumerable<Credential> RealVsizeCredentials)> ReissueCredentialAsync(
			Guid roundId,
			long value1,
			Script scriptPubKey1,
			long value2,
			Script scriptPubKey2,
			IEnumerable<Credential> amountCredentialsToPresent,
			IEnumerable<Credential> vsizeCredentialsToPresent)
		{
			Guard.InRange(nameof(amountCredentialsToPresent), amountCredentialsToPresent, 0, AmountCredentialClient.NumberOfCredentials);

			var presentedAmount = amountCredentialsToPresent.Sum(x => (long)x.Amount.ToUlong());
			if (value1 + value2 != presentedAmount)
			{
				throw new InvalidOperationException($"Reissuence amounts must equal with the sum of the presented ones.");
			}

			var presentedVsize = vsizeCredentialsToPresent.Sum(x => (long)x.Amount.ToUlong());
			var (realVsizeCredentialRequest, realVsizeCredentialResponseValidation) = VsizeCredentialClient.CreateRequest(
				new[] { (long)scriptPubKey1.EstimateOutputVsize(), scriptPubKey2.EstimateOutputVsize() },
				vsizeCredentialsToPresent);

			var (realAmountCredentialRequest, realAmountCredentialResponseValidation) = AmountCredentialClient.CreateRequest(
				new[] { value1, value2 },
				amountCredentialsToPresent);

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroVsizeCredentialRequestData = VsizeCredentialClient.CreateRequestForZeroAmount();

			var reissuanceResponse = await RequestHandler.ReissueCredentialAsync(
				new ReissueCredentialRequest(
					roundId,
					realAmountCredentialRequest,
					realVsizeCredentialRequest,
					zeroAmountCredentialRequestData.CredentialsRequest,
					zeroVsizeCredentialRequestData.CredentialsRequest)).ConfigureAwait(false);

			var realAmountCredentials = AmountCredentialClient.HandleResponse(reissuanceResponse.RealAmountCredentials, realAmountCredentialResponseValidation);
			var realVsizeCredentials = VsizeCredentialClient.HandleResponse(reissuanceResponse.RealVsizeCredentials, realVsizeCredentialResponseValidation);
			AmountCredentialClient.HandleResponse(reissuanceResponse.ZeroAmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			VsizeCredentialClient.HandleResponse(reissuanceResponse.ZeroVsizeCredentials, zeroVsizeCredentialRequestData.CredentialsResponseValidation);

			return (realAmountCredentials, realVsizeCredentials);
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

		public async Task SignTransactionAsync(Guid roundId, Coin coin, BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin)
		{
			if (unsignedCoinJoin.Inputs.Count == 0)
			{
				throw new ArgumentException("No inputs to sign.", nameof(unsignedCoinJoin));
			}

			var signedCoinJoin = unsignedCoinJoin.Clone();
			var txInput = signedCoinJoin.Inputs.AsIndexedInputs().FirstOrDefault(input => input.PrevOut == coin.Outpoint);

			if (txInput is null)
			{
				throw new InvalidOperationException($"Missing input.");
			}

			List<InputWitnessPair> signatures = new();

			signedCoinJoin.Sign(bitcoinSecret, coin);

			if (!txInput.VerifyScript(coin, out var error))
			{
				throw new InvalidOperationException($"Witness is missing. Reason {nameof(ScriptError)} code: {error}.");
			}

			signatures.Add(new InputWitnessPair(txInput.Index, txInput.WitScript));

			await RequestHandler.SignTransactionAsync(new TransactionSignaturesRequest(roundId, signatures)).ConfigureAwait(false);
		}
	}
}
