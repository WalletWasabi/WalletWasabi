using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class ArenaClient
	{
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

			var (zeroAmountCredentialRequest, zeroAmountCredentialResponseValidation) = AmountCredentialClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, zeroWeightCredentialResponseValidation) = WeightCredentialClient.CreateRequestForZeroAmount();

			var inputRegistrationResponse = await RequestHandler.RegisterInputAsync(
				new InputsRegistrationRequest(
					roundId,
					registrableInputs,
					zeroAmountCredentialRequest,
					zeroWeightCredentialRequest)).ConfigureAwait(false);

			AmountCredentialClient.HandleResponse(inputRegistrationResponse.AmountCredentials, zeroAmountCredentialResponseValidation);
			WeightCredentialClient.HandleResponse(inputRegistrationResponse.WeightCredentials, zeroWeightCredentialResponseValidation);

			return inputRegistrationResponse.AliceId;
		}

		public async Task ConfirmConnectionAsync(Guid roundId, Guid aliceId, IEnumerable<long> inputsRegistrationWeight, IEnumerable<Credential> credentialsToPresent, IEnumerable<Money> newAmount)
		{
			var (realAmountCredentialRequest, realAmountCredentialResponseValidation) = AmountCredentialClient.CreateRequest(
				newAmount.Select(x => x.Satoshi),
				credentialsToPresent);

			var (realWeightCredentialRequest, realWeightCredentialResponseValidation) = WeightCredentialClient.CreateRequest(
				inputsRegistrationWeight,
				WeightCredentialClient.Credentials.ZeroValue.Take(2));

			var (zeroAmountCredentialRequest, zeroAmountCredentialResponseValidation) = AmountCredentialClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, zeroWeightCredentialResponseValidation) = WeightCredentialClient.CreateRequestForZeroAmount();

			var confirmConnectionResponse = await RequestHandler.ConfirmConnectionAsync(
				new ConnectionConfirmationRequest(
					roundId,
					aliceId,
					zeroAmountCredentialRequest,
					realAmountCredentialRequest,
					zeroWeightCredentialRequest,
					realWeightCredentialRequest)).ConfigureAwait(false);

			if (confirmConnectionResponse is { RealAmountCredentials: { }, RealWeightCredentials: { } })
			{
				AmountCredentialClient.HandleResponse(confirmConnectionResponse.RealAmountCredentials, realAmountCredentialResponseValidation);
				WeightCredentialClient.HandleResponse(confirmConnectionResponse.RealWeightCredentials, realWeightCredentialResponseValidation);
			}
			AmountCredentialClient.HandleResponse(confirmConnectionResponse.ZeroAmountCredentials, zeroAmountCredentialResponseValidation);
			WeightCredentialClient.HandleResponse(confirmConnectionResponse.ZeroWeightCredentials, zeroWeightCredentialResponseValidation);
		}
	}
}
