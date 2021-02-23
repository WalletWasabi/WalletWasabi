using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
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

		public static InputRoundSignaturePair[] CreateInputRoundSignaturePairs(int count, uint256? roundHash = null)
		{
			List<InputRoundSignaturePair> pairs = new();
			for (int i = 0; i < count; i++)
			{
				pairs.Add(CreateInputRoundSignaturePair(null, roundHash));
			}

			return pairs.ToArray();
		}

		public static InputRoundSignaturePair[] CreateInputRoundSignaturePairs(IEnumerable<Key> keys, uint256? roundHash = null)
		{
			List<InputRoundSignaturePair> pairs = new();
			foreach (var key in keys)
			{
				pairs.Add(CreateInputRoundSignaturePair(key, roundHash));
			}
			return pairs.ToArray();
		}

		public static Round CreateRound(WabiSabiConfig cfg)
			=> new(new RoundParameters(
				cfg,
				Network.Main,
				new InsecureRandom(),
				new(100m)));

		public static async Task<Arena> CreateAndStartArenaAsync(params Round[] rounds)
		{
			Arena arena = new(TimeSpan.FromSeconds(1), rounds.FirstOrDefault()?.Network ?? Network.Main);
			foreach (var round in rounds)
			{
				arena.Rounds.Add(round.Id, round);
			}
			await arena.StartAsync(CancellationToken.None).ConfigureAwait(false);
			return arena;
		}

		public static Alice CreateAlice(InputRoundSignaturePair inputSigPairs, Key? key = null, Money? value = null) => CreateAlice(new[] { inputSigPairs }, key, value);

		public static Alice CreateAlice(IEnumerable<InputRoundSignaturePair>? inputSigPairs = null, Key? key = null, Money? value = null)
		{
			var pairs = inputSigPairs ?? CreateInputRoundSignaturePairs(1);
			var myDic = new Dictionary<Coin, byte[]>();

			foreach (var pair in pairs)
			{
				var coin = new Coin(pair.Input, new TxOut(value ?? Money.Coins(1), BitcoinFactory.CreateScript(key)));
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
			var wi = round?.WeightCredentialIssuer ?? new CredentialIssuer(new CredentialIssuerSecretKey(rnd), 2, rnd, 2000ul);
			var ac = new WabiSabiClient(
					ai.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
					ai.NumberOfCredentials,
					rnd,
					ai.MaxAmount);

			var wc = new WabiSabiClient(
					wi.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
					wi.NumberOfCredentials,
					rnd,
					wi.MaxAmount);

			return (ac, wc, ai, wi);
		}

		public static (Credential[] amountCredentials, IEnumerable<Credential> weightCredentials) CreateZeroCredentials(Round? round)
		{
			(var amClient, var weClient, var amIssuer, var weIssuer) = CreateWabiSabiClientsAndIssuers(round);
			return CreateZeroCredentials(amClient, weClient, amIssuer, weIssuer);
		}

		private static (Credential[] amountCredentials, IEnumerable<Credential> weightCredentials) CreateZeroCredentials(WabiSabiClient amClient, WabiSabiClient weClient, CredentialIssuer amIssuer, CredentialIssuer weIssuer)
		{
			var (zeroAmountCredentialRequest, amVal) = amClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, weVal) = weClient.CreateRequestForZeroAmount();
			var amCredResp = amIssuer.HandleRequest(zeroAmountCredentialRequest);
			var weCredResp = weIssuer.HandleRequest(zeroWeightCredentialRequest);
			amClient.HandleResponse(amCredResp, amVal);
			weClient.HandleResponse(weCredResp, weVal);
			return (amClient.Credentials.ZeroValue.Take(amIssuer.NumberOfCredentials).ToArray(), weClient.Credentials.ZeroValue.Take(weIssuer.NumberOfCredentials));
		}

		public static (RealCredentialsRequest amountReq, RealCredentialsRequest weightReq) CreateRealCredentialRequests(Round? round = null, Money? amount = null, long? weight = null)
		{
			(var amClient, var weClient, _, _) = CreateWabiSabiClientsAndIssuers(round);

			var zeroPresentables = CreateZeroCredentials(round);
			var alice = round?.Alices.FirstOrDefault();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				new[] { amount?.Satoshi ?? alice?.CalculateRemainingAmountCredentials(round!.FeeRate).Satoshi ?? 1000L },
				zeroPresentables.amountCredentials);
			var (realWeightCredentialRequest, _) = weClient.CreateRequest(
				new[] { weight ?? alice?.CalculateRemainingWeightCredentials(round!.RegistrableWeightCredentials) ?? 1000L },
				zeroPresentables.weightCredentials);

			return (realAmountCredentialRequest, realWeightCredentialRequest);
		}

		public static ConnectionConfirmationRequest CreateConnectionConfirmationRequest(Round? round = null)
		{
			(var amClient, var weClient, _, _) = CreateWabiSabiClientsAndIssuers(round);

			var zeroPresentables = CreateZeroCredentials(round);
			var alice = round?.Alices.FirstOrDefault();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				new[] { alice?.CalculateRemainingAmountCredentials(round!.FeeRate).Satoshi ?? 1000L },
				zeroPresentables.amountCredentials);
			var (realWeightCredentialRequest, _) = weClient.CreateRequest(
				new[] { alice?.CalculateRemainingWeightCredentials(round!.RegistrableWeightCredentials) ?? 1000L },
				zeroPresentables.weightCredentials);

			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroWeightCredentialRequest, _) = weClient.CreateRequestForZeroAmount();

			return new ConnectionConfirmationRequest(
				round?.Id ?? Guid.NewGuid(),
				alice?.Id ?? Guid.NewGuid(),
				zeroAmountCredentialRequest,
				realAmountCredentialRequest,
				zeroWeightCredentialRequest,
				realWeightCredentialRequest);
		}

		public static OutputRegistrationRequest CreateOutputRegistrationRequest(Round? round = null, Script? script = null, int? weight = null)
		{
			(var amClient, var weClient, var amIssuer, var weIssuer) = CreateWabiSabiClientsAndIssuers(round);
			var zeroPresentables = CreateZeroCredentials(amClient, weClient, amIssuer, weIssuer);

			var alice = round?.Alices.FirstOrDefault();
			var (amCredentialRequest, amValid) = amClient.CreateRequest(
				new[] { alice?.CalculateRemainingAmountCredentials(round!.FeeRate).Satoshi ?? 1000L },
				zeroPresentables.amountCredentials);
			long startingWeightCredentialAmount = alice?.CalculateRemainingWeightCredentials(round!.RegistrableWeightCredentials) ?? 1000L;
			var (weCredentialRequest, weValid) = weClient.CreateRequest(
				new[] { startingWeightCredentialAmount },
				zeroPresentables.weightCredentials);

			var amResp = amIssuer.HandleRequest(amCredentialRequest);
			var weResp = weIssuer.HandleRequest(weCredentialRequest);
			amClient.HandleResponse(amResp, amValid);
			weClient.HandleResponse(weResp, weValid);

			script ??= BitcoinFactory.CreateScript();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				Array.Empty<long>(),
				amClient.Credentials.Valuable);

			try
			{
				weight ??= script.EstimateOutputVsize() * 4;
			}
			catch (NotImplementedException)
			{
				weight = 100;
			}

			var (realWeightCredentialRequest, _) = weClient.CreateRequest(
				new[] { startingWeightCredentialAmount - (long)weight },
				weClient.Credentials.Valuable);

			return new OutputRegistrationRequest(
				round?.Id ?? Guid.NewGuid(),
				script,
				realAmountCredentialRequest,
				realWeightCredentialRequest);
		}
	}
}
