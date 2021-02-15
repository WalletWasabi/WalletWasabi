using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Tests.Helpers
{
	public static class WabiSabiFactory
	{
		public static InputRoundSignaturePair CreateInputRoundSignaturePair(Key? key = null, uint256? roundHash = null)
		{
			var rh = roundHash ?? BitcoinFactory.CreateUint256();
			if (key is null)
			{
				using Key k = new();
				return new InputRoundSignaturePair(
						BitcoinFactory.CreateOutPoint(),
						k.SignCompact(rh));
			}
			else
			{
				return new InputRoundSignaturePair(
						BitcoinFactory.CreateOutPoint(),
						key.SignCompact(rh));
			}
		}

		public static IEnumerable<InputRoundSignaturePair> CreateInputRoundSignaturePairs(int count, uint256? roundHash = null)
		{
			for (int i = 0; i < count; i++)
			{
				yield return CreateInputRoundSignaturePair(null, roundHash);
			}
		}

		public static IEnumerable<InputRoundSignaturePair> CreateInputRoundSignaturePairs(IEnumerable<Key> keys, uint256? roundHash = null)
		{
			foreach (var key in keys)
			{
				yield return CreateInputRoundSignaturePair(key, roundHash);
			}
		}

		public static Round CreateRound(WabiSabiConfig cfg)
			=> new Round(
				cfg.MaxInputCountByAlice,
				cfg.MinRegistrableAmount,
				cfg.MaxRegistrableAmount,
				cfg.MinRegistrableWeight,
				cfg.MaxRegistrableWeight);

		public static InputsRegistrationRequest CreateInputsRegistrationRequest(Key key, Round? round)
			=> CreateInputsRegistrationRequest(new[] { key }, round);

		public static InputsRegistrationRequest CreateInputsRegistrationRequest(IEnumerable<Key>? keys, Round? round)
		{
			var pairs = keys is null
				? CreateInputRoundSignaturePairs(1, round?.Hash)
				: CreateInputRoundSignaturePairs(keys, round?.Hash);
			return CreateInputsRegistrationRequest(pairs, round);
		}

		public static InputsRegistrationRequest CreateInputsRegistrationRequest(InputRoundSignaturePair pair, Round? round)
			=> CreateInputsRegistrationRequest(new[] { pair }, round);

		public static InputsRegistrationRequest CreateInputsRegistrationRequest(Round? round)
			=> CreateInputsRegistrationRequest(CreateInputRoundSignaturePairs(1, round?.Hash), round);

		public static InputsRegistrationRequest CreateInputsRegistrationRequest()
			=> CreateInputsRegistrationRequest(pairs: null, round: null);

		public static InputsRegistrationRequest CreateInputsRegistrationRequest(IEnumerable<InputRoundSignaturePair>? pairs, Round? round)
		{
			var roundId = round?.Id ?? Guid.NewGuid();
			var inputRoundSignaturePairs = pairs ?? CreateInputRoundSignaturePairs(1, round?.Hash);
			var rnd = new InsecureRandom();

			var client = new WabiSabiClient(new CredentialIssuerSecretKey(rnd).ComputeCredentialIssuerParameters(), 2, rnd, 4300000000000);         // Input Reg
			var (zeroAmountCredentialRequest, _) = client.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, _) = client.CreateRequestForZeroAmount();

			return new(
				roundId,
				inputRoundSignaturePairs,
				zeroAmountCredentialRequest,
				zeroWeightCredentialRequest);
		}
	}
}
