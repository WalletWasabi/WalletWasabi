using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
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

		public async Task SignTransactionAsync(Guid roundId, ICoin[] coinsToSign, BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin)
		{
			// TODO: Sanity check on the CoinJoin.

			List<InputWitnessPair> signatures = new();

			var signedCoinJoin = unsignedCoinJoin.Clone();
			foreach (var coin in coinsToSign)
			{
				signedCoinJoin.Sign(bitcoinSecret, coin);
			}

			var myInputs = coinsToSign.Select(c => c.Outpoint).ToHashSet();

			for (uint i = 0; i < signedCoinJoin.Inputs.Count; i++)
			{
				var input = signedCoinJoin.Inputs[i];
				if (myInputs.Contains(input.PrevOut))
				{
					signatures.Add(new InputWitnessPair(i, signedCoinJoin.Inputs[i].WitScript));
				}
			}

			await RequestHandler.SignTransactionAsync(new TransactionSignaturesRequest(roundId, signatures)).ConfigureAwait(false);
		}
	}
}
