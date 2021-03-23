using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class WabiSabiApiClient
	{
		public WabiSabiApiClient(WabiSabiClient wabiSabiClientAmount, WabiSabiClient wabiSabiClientWeight, IRequestHandler requestHandler)
		{
			WabiSabiClientAmount = wabiSabiClientAmount;
			WabiSabiClientWeight = wabiSabiClientWeight;
			RequestHandler = requestHandler;
		}

		public WabiSabiClient WabiSabiClientAmount { get; }
		public WabiSabiClient WabiSabiClientWeight { get; }
		public IRequestHandler RequestHandler { get; }

		public Task RegisterInputAsync(Money amount, OutPoint outPoint, Key key, Guid roundId, uint256 roundHash) =>
			RegisterInputAsync(
				new[] { amount },
				new[] { outPoint },
				new[] { key },
				roundId,
				roundHash);
				
		public async Task RegisterInputAsync(
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

			var (zeroAmountCredentialRequest, zeroAmountCredentialResponseValidation) = WabiSabiClientAmount.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, zeroWeightCredentialResponseValidation) = WabiSabiClientWeight.CreateRequestForZeroAmount();

			var inputRegistrationResponse = await RequestHandler.RegisterInputAsync(
				new InputsRegistrationRequest(
					roundId,
					registrableInputs,
					zeroAmountCredentialRequest,
					zeroWeightCredentialRequest));

			WabiSabiClientAmount.HandleResponse(inputRegistrationResponse.AmountCredentials, zeroAmountCredentialResponseValidation);
			WabiSabiClientWeight.HandleResponse(inputRegistrationResponse.WeightCredentials, zeroWeightCredentialResponseValidation);
		}
	}
}