using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
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
				Network.Main,
				cfg.MaxInputCountByAlice,
				cfg.MinRegistrableAmount,
				cfg.MaxRegistrableAmount,
				cfg.MinRegistrableWeight,
				cfg.MaxRegistrableWeight,
				cfg.ConnectionConfirmationTimeout,
				cfg.OutputRegistrationTimeout,
				cfg.TransactionSigningTimeout,
				new InsecureRandom());

		public static Alice CreateAlice(InputRoundSignaturePair inputSigPairs) => CreateAlice(new[] { inputSigPairs });

		public static Alice CreateAlice(IEnumerable<InputRoundSignaturePair>? inputSigPairs = null)
		{
			var pairs = inputSigPairs ?? CreateInputRoundSignaturePairs(1);
			var myDic = new Dictionary<Coin, byte[]>();

			foreach (var pair in pairs)
			{
				var coin = new Coin(pair.Input, new TxOut(Money.Coins(1), BitcoinFactory.CreateScript()));
				myDic.Add(coin, pair.RoundSignature);
			}
			return new Alice(myDic);
		}

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

			(var amClient, var weClient, _, _) = CreateWabiSabiClientsAndIssuers(round);
			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, _) = weClient.CreateRequestForZeroAmount();

			return new(
				roundId,
				inputRoundSignaturePairs,
				zeroAmountCredentialRequest,
				zeroWeightCredentialRequest);
		}

		public static (WabiSabiClient amountClient, WabiSabiClient weightClient, CredentialIssuer amountIssuer, CredentialIssuer weightIssuer) CreateWabiSabiClientsAndIssuers(Round? round)
		{
			var rnd = new InsecureRandom();
			var ai = round?.AmountCredentialIssuer ?? new CredentialIssuer(new CredentialIssuerSecretKey(rnd), 2, rnd, 4300000000000);
			var wi = round?.WeightCredentialIssuer ?? new CredentialIssuer(new CredentialIssuerSecretKey(rnd), 2, rnd, 4300000000000);
			var ac = new WabiSabiClient(
					ai.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
					ai.NumberOfCredentials,
					rnd,
					round?.MaxRegistrableAmountByAlice ?? 4300000000000);

			var wc = new WabiSabiClient(
					wi.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
					wi.NumberOfCredentials,
					rnd,
					round?.MaxRegistrableWeightByAlice ?? 4300000000000ul);

			return (ac, wc, ai, wi);
		}

		public static (IEnumerable<Credential> amountCredentials, IEnumerable<Credential> weightCredentials) CreateZeroCredentials(Round? round)
		{
			(var amClient, var weClient, var amIssuer, var weIssuer) = CreateWabiSabiClientsAndIssuers(round);

			var (zeroAmountCredentialRequest, amVal) = amClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, weVal) = weClient.CreateRequestForZeroAmount();
			var amCredResp = amIssuer.HandleRequest(zeroAmountCredentialRequest);
			var weCredResp = weIssuer.HandleRequest(zeroWeightCredentialRequest);
			amClient.HandleResponse(amCredResp, amVal);
			weClient.HandleResponse(weCredResp, weVal);
			return (amClient.Credentials.ZeroValue.Take(amIssuer.NumberOfCredentials), weClient.Credentials.ZeroValue.Take(weIssuer.NumberOfCredentials));
		}

		public static ConnectionConfirmationRequest CreateConnectionConfirmationRequest(Round? round = null)
		{
			(var amClient, var weClient, _, _) = CreateWabiSabiClientsAndIssuers(round);

			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, _) = weClient.CreateRequestForZeroAmount();

			var zeroPresentables = CreateZeroCredentials(round);
			var alice = round?.Alices.FirstOrDefault();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				alice is null ? new[] { 1000L } : new[] { alice.Coins.Select(x => x.Amount.Satoshi).Sum() },
				zeroPresentables.amountCredentials);
			var (realWeightCredentialRequest, _) = weClient.CreateRequest(
				new[] { 1000L },
				zeroPresentables.weightCredentials);

			return new ConnectionConfirmationRequest(
				round?.Id ?? Guid.NewGuid(),
				alice?.Id ?? Guid.NewGuid(),
				zeroAmountCredentialRequest,
				realAmountCredentialRequest,
				zeroWeightCredentialRequest,
				realWeightCredentialRequest);
		}
	}
}
