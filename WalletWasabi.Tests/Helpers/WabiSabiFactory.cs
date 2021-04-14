using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.UnitTests;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi;

namespace WalletWasabi.Tests.Helpers
{
	public static class WabiSabiFactory
	{
		public static InputRoundSignaturePair CreateInputRoundSignaturePair(Key? key = null, uint256? roundHash = null)
		{
			var rh = roundHash ?? BitcoinFactory.CreateUint256();
			var outpoint = BitcoinFactory.CreateOutPoint();
			var coinJoinInputCommitmentData = new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", rh);

			var signingKey = key ?? new();
			return new InputRoundSignaturePair(
				outpoint,
				OwnershipProof.GenerateCoinJoinInputProof(signingKey, coinJoinInputCommitmentData).ToBytes());
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

		public static async Task<Arena> CreateAndStartArenaAsync(WabiSabiConfig cfg, params Round[] rounds)
			=> await CreateAndStartArenaAsync(cfg, null, rounds);

		public static async Task<Arena> CreateAndStartArenaAsync(WabiSabiConfig? cfg = null, MockRpcClient? mockRpc = null, params Round[] rounds)
		{
			mockRpc ??= new MockRpcClient();
			mockRpc.OnEstimateSmartFeeAsync ??= async (target, _) =>
				await Task.FromResult(new EstimateSmartFeeResponse
				{
					Blocks = target,
					FeeRate = new FeeRate(10m)
				});
			mockRpc.OnSendRawTransactionAsync ??= (tx) => tx.GetHash();
			mockRpc.OnGetTxOutAsync ??= (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new(Money.Coins(1), Script.Empty)
			};

			Arena arena = new(TimeSpan.FromHours(1), rounds.FirstOrDefault()?.Network ?? Network.Main, cfg ?? new WabiSabiConfig(), mockRpc, new Prison());
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
			var alice = new Alice(myDic);
			alice.Deadline = DateTimeOffset.UtcNow + TimeSpan.FromHours(1);
			return alice;
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

			(var amClient, var vsClient, _, _) = CreateWabiSabiClientsAndIssuers(round);
			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, _) = vsClient.CreateRequestForZeroAmount();

			return new(
				roundId,
				inputRoundSignaturePairs,
				zeroAmountCredentialRequest,
				zeroVsizeCredentialRequest);
		}

		public static (WabiSabiClient amountClient, WabiSabiClient vsizeClient, CredentialIssuer amountIssuer, CredentialIssuer vsizeIssuer) CreateWabiSabiClientsAndIssuers(Round? round)
		{
			var rnd = new InsecureRandom();
			var ai = round?.AmountCredentialIssuer ?? new CredentialIssuer(new CredentialIssuerSecretKey(rnd), 2, rnd, 4300000000000);
			var wi = round?.VsizeCredentialIssuer ?? new CredentialIssuer(new CredentialIssuerSecretKey(rnd), 2, rnd, 2000ul);
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

		public static (Credential[] amountCredentials, IEnumerable<Credential> vsizeCredentials) CreateZeroCredentials(Round? round)
		{
			(var amClient, var vsClient, var amIssuer, var vsIssuer) = CreateWabiSabiClientsAndIssuers(round);
			return CreateZeroCredentials(amClient, vsClient, amIssuer, vsIssuer);
		}

		private static (Credential[] amountCredentials, IEnumerable<Credential> vsizeCredentials) CreateZeroCredentials(WabiSabiClient amClient, WabiSabiClient vsClient, CredentialIssuer amIssuer, CredentialIssuer vsIssuer)
		{
			var (zeroAmountCredentialRequest, amVal) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, vsVal) = vsClient.CreateRequestForZeroAmount();
			var amCredResp = amIssuer.HandleRequest(zeroAmountCredentialRequest);
			var vsCredResp = vsIssuer.HandleRequest(zeroVsizeCredentialRequest);
			amClient.HandleResponse(amCredResp, amVal);
			vsClient.HandleResponse(vsCredResp, vsVal);
			return (amClient.Credentials.ZeroValue.Take(amIssuer.NumberOfCredentials).ToArray(), vsClient.Credentials.ZeroValue.Take(vsIssuer.NumberOfCredentials));
		}

		public static (RealCredentialsRequest amountReq, RealCredentialsRequest vsizeReq) CreateRealCredentialRequests(Round? round = null, Money? amount = null, long? vsize = null)
		{
			(var amClient, var vsClient, _, _) = CreateWabiSabiClientsAndIssuers(round);

			var zeroPresentables = CreateZeroCredentials(round);
			var alice = round?.Alices.FirstOrDefault();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				new[] { amount?.Satoshi ?? alice?.CalculateRemainingAmountCredentials(round!.FeeRate).Satoshi ?? ProtocolConstants.MaxVsizePerAlice },
				zeroPresentables.amountCredentials);
			var (realVsizeCredentialRequest, _) = vsClient.CreateRequest(
				new[] { vsize ?? alice?.CalculateRemainingSizeCredentials(round!.PerAliceVsizeAllocation) ?? ProtocolConstants.MaxVsizePerAlice },
				zeroPresentables.vsizeCredentials);

			return (realAmountCredentialRequest, realVsizeCredentialRequest);
		}

		public static ConnectionConfirmationRequest CreateConnectionConfirmationRequest(Round? round = null)
		{
			(var amClient, var vsClient, _, _) = CreateWabiSabiClientsAndIssuers(round);

			var zeroPresentables = CreateZeroCredentials(round);
			var alice = round?.Alices.FirstOrDefault();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				new[] { alice?.CalculateRemainingAmountCredentials(round!.FeeRate).Satoshi ?? ProtocolConstants.MaxVsizePerAlice },
				zeroPresentables.amountCredentials);
			var (realVsizeCredentialRequest, _) = vsClient.CreateRequest(
				new[] { alice?.CalculateRemainingSizeCredentials(round!.PerAliceVsizeAllocation) ?? ProtocolConstants.MaxVsizePerAlice },
				zeroPresentables.vsizeCredentials);

			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, _) = vsClient.CreateRequestForZeroAmount();

			return new ConnectionConfirmationRequest(
				round?.Id ?? Guid.NewGuid(),
				alice?.Id ?? Guid.NewGuid(),
				zeroAmountCredentialRequest,
				realAmountCredentialRequest,
				zeroVsizeCredentialRequest,
				realVsizeCredentialRequest);
		}

		public static IEnumerable<(ConnectionConfirmationRequest request, WabiSabiClient amountClient, WabiSabiClient vsizeClient, CredentialsResponseValidation amountValidation, CredentialsResponseValidation vsizeValidation)> CreateConnectionConfirmationRequests(Round round, params InputsRegistrationResponse[] responses)
		{
			var requests = new List<(ConnectionConfirmationRequest request, WabiSabiClient amountClient, WabiSabiClient vsizeClient, CredentialsResponseValidation amountValidation, CredentialsResponseValidation vsizeValidation)>();
			foreach (var resp in responses)
			{
				requests.Add(CreateConnectionConfirmationRequest(round, resp));
			}

			return requests.ToArray();
		}

		public static (ConnectionConfirmationRequest request, WabiSabiClient amountClient, WabiSabiClient vsizeClient, CredentialsResponseValidation amountValidation, CredentialsResponseValidation vsizeValidation) CreateConnectionConfirmationRequest(Round round, InputsRegistrationResponse response)
		{
			(var amClient, var vsClient, _, _) = CreateWabiSabiClientsAndIssuers(round);

			var zeroPresentables = CreateZeroCredentials(round);
			var alice = round.Alices.First(x => x.Id == response.AliceId);
			var (realAmountCredentialRequest, amVal) = amClient.CreateRequest(
				new[] { alice.CalculateRemainingAmountCredentials(round.FeeRate).Satoshi },
				zeroPresentables.amountCredentials);
			var (realVsizeCredentialRequest, weVal) = vsClient.CreateRequest(
				new[] { alice.CalculateRemainingSizeCredentials(round.PerAliceVsizeAllocation) },
				zeroPresentables.vsizeCredentials);

			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, _) = vsClient.CreateRequestForZeroAmount();

			return (
				new ConnectionConfirmationRequest(
					round.Id,
					response.AliceId,
					zeroAmountCredentialRequest,
					realAmountCredentialRequest,
					zeroVsizeCredentialRequest,
					realVsizeCredentialRequest),
				amClient,
				vsClient,
				amVal,
				weVal);
		}

		public static OutputRegistrationRequest CreateOutputRegistrationRequest(Round? round = null, Script? script = null, int? vsize = null)
		{
			(var amClient, var vsClient, var amIssuer, var vsIssuer) = CreateWabiSabiClientsAndIssuers(round);
			var zeroPresentables = CreateZeroCredentials(amClient, vsClient, amIssuer, vsIssuer);

			var alice = round?.Alices.FirstOrDefault();
			var (amCredentialRequest, amValid) = amClient.CreateRequest(
				new[] { alice?.CalculateRemainingAmountCredentials(round!.FeeRate).Satoshi ?? ProtocolConstants.MaxVsizePerAlice },
				zeroPresentables.amountCredentials);
			long startingWeightCredentialAmount = alice?.CalculateRemainingSizeCredentials(round!.PerAliceVsizeAllocation) ?? ProtocolConstants.MaxVsizePerAlice;
			var (vsCredentialRequest, weValid) = vsClient.CreateRequest(
				new[] { startingWeightCredentialAmount },
				zeroPresentables.vsizeCredentials);

			var amResp = amIssuer.HandleRequest(amCredentialRequest);
			var weResp = vsIssuer.HandleRequest(vsCredentialRequest);
			amClient.HandleResponse(amResp, amValid);
			vsClient.HandleResponse(weResp, weValid);

			script ??= BitcoinFactory.CreateScript();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				Array.Empty<long>(),
				amClient.Credentials.Valuable);

			try
			{
				vsize ??= script.EstimateOutputVsize();
			}
			catch (NotImplementedException)
			{
				vsize = 100; // ??
			}

			var (realVsizeCredentialRequest, _) = vsClient.CreateRequest(
				new[] { startingWeightCredentialAmount - (long)vsize },
				vsClient.Credentials.Valuable);

			return new OutputRegistrationRequest(
				round?.Id ?? Guid.NewGuid(),
				script,
				realAmountCredentialRequest,
				realVsizeCredentialRequest);
		}

		public static IEnumerable<OutputRegistrationRequest> CreateOutputRegistrationRequests(Round round, IEnumerable<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient vsizeClient, Guid aliceId)> ccresps)
		{
			var ret = new List<OutputRegistrationRequest>();

			foreach (var ccresp in ccresps)
			{
				var alice = round.Alices.First(x => x.Id == ccresp.aliceId);
				var startingVsizeCredentialAmount = alice.CalculateRemainingSizeCredentials(round!.PerAliceVsizeAllocation);
				var script = BitcoinFactory.CreateScript();
				var vsize = script.EstimateOutputVsize();
				ret.Add(new OutputRegistrationRequest(
					round.Id,
					script,
					ccresp.amountClient.CreateRequest(Array.Empty<long>(), ccresp.amountClient.Credentials.Valuable).CredentialsRequest,
					ccresp.vsizeClient.CreateRequest(new[] { startingVsizeCredentialAmount - vsize }, ccresp.vsizeClient.Credentials.Valuable).CredentialsRequest));
			}

			return ret;
		}

		public static Round CreateBlameRound(Round round, WabiSabiConfig cfg)
		{
			RoundParameters parameters = new(cfg, round.Network, round.Random, round.FeeRate, blameOf: round);
			return new(parameters);
		}

		public static MockRpcClient CreateMockRpc(Key? key = null)
		{
			MockRpcClient rpc = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new(Money.Coins(1), key?.PubKey.GetSegwitAddress(Network.Main).ScriptPubKey ?? Script.Empty)
			};

			return rpc;
		}
	}
}
