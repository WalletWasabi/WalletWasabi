using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
			ZeroCredentialPool zeroAmountCredentialPool,
			ZeroCredentialPool zeroVsizeCredentialPool,
			IArenaRequestHandler requestHandler,
			WasabiRandom random)
		{
			AmountCredentialClient = new WabiSabiClient(amountCredentialIssuerParameters, random, ProtocolConstants.MaxAmountPerAlice, zeroAmountCredentialPool);
			VsizeCredentialClient = new WabiSabiClient(vsizeCredentialIssuerParameters, random, ProtocolConstants.MaxVsizePerAlice, zeroVsizeCredentialPool);
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

		public async Task<ArenaResponse<uint256>> RegisterInputAsync(
			Money amount,
			OutPoint outPoint,
			Key key,
			uint256 roundId,
			CancellationToken cancellationToken)
		{
			var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
				key,
				new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundId));

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroVsizeCredentialRequestData = VsizeCredentialClient.CreateRequestForZeroAmount();

			var inputRegistrationResponse = await RequestHandler.RegisterInputAsync(
				new InputRegistrationRequest(
					roundId,
					outPoint,
					ownershipProof,
					zeroAmountCredentialRequestData.CredentialsRequest,
					zeroVsizeCredentialRequestData.CredentialsRequest),
				cancellationToken).ConfigureAwait(false);

			var realAmountCredentials = AmountCredentialClient.HandleResponse(inputRegistrationResponse.AmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			var realVsizeCredentials  = VsizeCredentialClient.HandleResponse(inputRegistrationResponse.VsizeCredentials, zeroVsizeCredentialRequestData.CredentialsResponseValidation);

			return new (inputRegistrationResponse.AliceId, realAmountCredentials, realVsizeCredentials);
		}

		public async Task RemoveInputAsync(uint256 roundId, uint256 aliceId, CancellationToken cancellationToken)
		{
			await RequestHandler.RemoveInputAsync(new InputsRemovalRequest(roundId, aliceId), cancellationToken).ConfigureAwait(false);
		}

		public async Task<ArenaResponse> RegisterOutputAsync(
			uint256 roundId,
			long value,
			Script scriptPubKey,
			IEnumerable<Credential> amountCredentialsToPresent,
			IEnumerable<Credential> vsizeCredentialsToPresent,
			CancellationToken cancellationToken)
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
					realVsizeCredentialRequest),
				cancellationToken).ConfigureAwait(false);

			var realAmountCredentials = AmountCredentialClient.HandleResponse(outputRegistrationResponse.AmountCredentials, realAmountCredentialResponseValidation);
			var realVsizeCredentials = VsizeCredentialClient.HandleResponse(outputRegistrationResponse.VsizeCredentials, realVsizeCredentialResponseValidation);

			return new (realAmountCredentials, realVsizeCredentials);
		}

		public async Task<ArenaResponse> ReissueCredentialAsync(
			uint256 roundId,
			long value1,
			Script scriptPubKey1,
			long value2,
			Script scriptPubKey2,
			IEnumerable<Credential> amountCredentialsToPresent,
			IEnumerable<Credential> vsizeCredentialsToPresent,
			CancellationToken cancellationToken)
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
					zeroVsizeCredentialRequestData.CredentialsRequest),
				cancellationToken).ConfigureAwait(false);

			var realAmountCredentials = AmountCredentialClient.HandleResponse(reissuanceResponse.RealAmountCredentials, realAmountCredentialResponseValidation);
			var realVsizeCredentials = VsizeCredentialClient.HandleResponse(reissuanceResponse.RealVsizeCredentials, realVsizeCredentialResponseValidation);
			AmountCredentialClient.HandleResponse(reissuanceResponse.ZeroAmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			VsizeCredentialClient.HandleResponse(reissuanceResponse.ZeroVsizeCredentials, zeroVsizeCredentialRequestData.CredentialsResponseValidation);

			return new (realAmountCredentials, realVsizeCredentials);
		}

		public async Task<ArenaResponse<bool>> ConfirmConnectionAsync(
			uint256 roundId,
			uint256 aliceId,
			IEnumerable<long> inputsRegistrationVsize,
			IEnumerable<Credential> amountCredentialsToPresent,
			IEnumerable<Credential> vsizeCredentialsToPresent,
			IEnumerable<Money> newAmount,
			CancellationToken cancellationToken)
		{
			Guard.InRange(nameof(newAmount), newAmount, 1, ProtocolConstants.CredentialNumber);
			Guard.InRange(nameof(amountCredentialsToPresent), amountCredentialsToPresent, 0, ProtocolConstants.CredentialNumber);
			Guard.InRange(nameof(vsizeCredentialsToPresent), vsizeCredentialsToPresent, 0, ProtocolConstants.CredentialNumber);
			Guard.InRange(nameof(inputsRegistrationVsize), inputsRegistrationVsize, 1, VsizeCredentialClient.NumberOfCredentials);

			var realAmountCredentialRequestData = AmountCredentialClient.CreateRequest(
				newAmount.Select(x => x.Satoshi),
				amountCredentialsToPresent);

			var realVsizeCredentialRequestData = VsizeCredentialClient.CreateRequest(
				inputsRegistrationVsize,
				vsizeCredentialsToPresent);

			var zeroAmountCredentialRequestData = AmountCredentialClient.CreateRequestForZeroAmount();
			var zeroVsizeCredentialRequestData = VsizeCredentialClient.CreateRequestForZeroAmount();

			var confirmConnectionResponse = await RequestHandler.ConfirmConnectionAsync(
				new ConnectionConfirmationRequest(
					roundId,
					aliceId,
					zeroAmountCredentialRequestData.CredentialsRequest,
					realAmountCredentialRequestData.CredentialsRequest,
					zeroVsizeCredentialRequestData.CredentialsRequest,
					realVsizeCredentialRequestData.CredentialsRequest),
				cancellationToken).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(confirmConnectionResponse.ZeroAmountCredentials, zeroAmountCredentialRequestData.CredentialsResponseValidation);
			VsizeCredentialClient.HandleResponse(confirmConnectionResponse.ZeroVsizeCredentials, zeroVsizeCredentialRequestData.CredentialsResponseValidation);

			if (confirmConnectionResponse is { RealAmountCredentials: { }, RealVsizeCredentials: { } })
			{
				var realAmountCredentials = AmountCredentialClient.HandleResponse(confirmConnectionResponse.RealAmountCredentials, realAmountCredentialRequestData.CredentialsResponseValidation);
				var realVsizeCredentials = VsizeCredentialClient.HandleResponse(confirmConnectionResponse.RealVsizeCredentials, realVsizeCredentialRequestData.CredentialsResponseValidation);
				return new (true, realAmountCredentials, realVsizeCredentials);
			}

			return new (false, Enumerable.Empty<Credential>(), Enumerable.Empty<Credential>());
		}

		public async Task SignTransactionAsync(uint256 roundId, Coin coin, BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin, CancellationToken cancellationToken)
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

			await RequestHandler.SignTransactionAsync(new TransactionSignaturesRequest(roundId, signatures), cancellationToken).ConfigureAwait(false);
		}
	}
}
