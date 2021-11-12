using Moq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.Tests.Helpers
{
	public static class WabiSabiFactory
	{
		public static Coin CreateCoin(Key key)
			=> CreateCoin(key, Money.Coins(1));

		public static Coin CreateCoin(Key key, Money amount)
			=> new(
				new OutPoint(Hashes.DoubleSHA256(key.PubKey.ToBytes()), 0),
				new TxOut(amount, key.PubKey.WitHash.ScriptPubKey));

		public static OwnershipProof CreateOwnershipProof(Key key, uint256? roundHash = null)
			=> OwnershipProof.GenerateCoinJoinInputProof(
				key,
				GetOwnershipIdentifier(key.PubKey.WitHash.ScriptPubKey),
				new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundHash ?? BitcoinFactory.CreateUint256()));

		public static OwnershipIdentifier GetOwnershipIdentifier(Script scriptPubKey)
		{
			using var identificationKey = Key.Parse("5KbdaBwc9Eit2LrmDp1WfZd815StNstwHanbRrPpGGN6wWJKyHe", Network.Main);
			return new OwnershipIdentifier(identificationKey, scriptPubKey);
		}

		public static Round CreateRound(WabiSabiConfig cfg)
		{
			Round round = new(new RoundParameters(
				cfg,
				Network.Main,
				new InsecureRandom(),
				new FeeRate(100m)));
			round.MaxVsizeAllocationPerAlice = 11 + 31 + MultipartyTransactionParameters.SharedOverhead;
			return round;
		}

		public static Mock<IRPCClient> CreatePreconfiguredRpcClient(params Coin[] coins)
		{
			using Key key = new();
			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(It.IsAny<uint256>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					Confirmations = 120,
					TxOut = new TxOut(Money.Coins(1), BitcoinFactory.CreateScript()),
				});
			foreach (var coin in coins)
			{
				mockRpc.Setup(rpc => rpc.GetTxOutAsync(coin.Outpoint.Hash, (int)coin.Outpoint.N, true, It.IsAny<CancellationToken>()))
					.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
					{
						IsCoinBase = false,
						ScriptPubKeyType = "witness_v0_keyhash",
						Confirmations = 120,
						TxOut = coin.TxOut,
					});
			}
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new EstimateSmartFeeResponse
				{
					Blocks = 1000,
					FeeRate = new FeeRate(10m)
				});
			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new MemPoolInfo
				{
					MinRelayTxFee = 1
				});
			mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);
			mockRpc.Setup(rpc => rpc.SendBatchAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
			return mockRpc;
		}

		public static async Task<Arena> CreateAndStartArenaAsync()
			=> await CreateAndStartArenaAsync(
				new WabiSabiConfig(),
				CreatePreconfiguredRpcClient());

		public static async Task<Arena> CreateAndStartArenaAsync(WabiSabiConfig cfg, params Round[] rounds)
			=> await CreateAndStartArenaAsync(
				cfg,
				CreatePreconfiguredRpcClient(),
				rounds);

		public static async Task<Arena> CreateAndStartArenaAsync(WabiSabiConfig cfg, IMock<IRPCClient> mockRpc, params Round[] rounds)
		{
			Arena arena = new(TimeSpan.FromHours(1), Network.Main, cfg, mockRpc.Object, new Prison());
			foreach (var round in rounds)
			{
				arena.Rounds.Add(round);
			}
			await arena.StartAsync(CancellationToken.None).ConfigureAwait(false);
			return arena;
		}

		public static Alice CreateAlice(Coin coin, OwnershipProof ownershipProof, Round round)
			=> new(coin, ownershipProof, round, Guid.NewGuid()) { Deadline = DateTimeOffset.UtcNow + TimeSpan.FromHours(1) };

		public static Alice CreateAlice(Key key, Money amount, Round round)
			=> CreateAlice(CreateCoin(key, amount), CreateOwnershipProof(key), round);

		public static Alice CreateAlice(Money amount, Round round)
			=> CreateAlice(new Key(), amount, round);

		public static Alice CreateAlice(Key key, Round round)
			=> CreateAlice(key, Money.Coins(1), round);

		public static Alice CreateAlice(Round round)
			=> CreateAlice(new Key(), Money.Coins(1), round);

		public static ArenaClient CreateArenaClient(Arena arena)
		{
			var roundState = RoundState.FromRound(arena.Rounds.First());
			var random = new InsecureRandom();
			return new ArenaClient(
				roundState.CreateAmountCredentialClient(random),
				roundState.CreateVsizeCredentialClient(random),
				arena);
		}

		public static InputRegistrationRequest CreateInputRegistrationRequest(Round round, Key? key = null, OutPoint? prevout = null)
		{
			(var amClient, var vsClient, _, _, _, _) = CreateWabiSabiClientsAndIssuers(round);
			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, _) = vsClient.CreateRequestForZeroAmount();

			return new(
				round.Id,
				prevout ?? BitcoinFactory.CreateOutPoint(),
				CreateOwnershipProof(key ?? new Key(), round.Id),
				zeroAmountCredentialRequest,
				zeroVsizeCredentialRequest);
		}

		public static (
			WabiSabiClient amountClient,
			WabiSabiClient vsizeClient,
			CredentialIssuer amountIssuer,
			CredentialIssuer vsizeIssuer,
			IEnumerable<Credential> amountZeroCredentials,
			IEnumerable<Credential> vsizeZeroCredentials
		) CreateWabiSabiClientsAndIssuers(Round round)
		{
			var rnd = new InsecureRandom();
			var amountIssuer = round.AmountCredentialIssuer;
			var vsizeIssuer = round.VsizeCredentialIssuer;
			var amountClient = new WabiSabiClient(
				amountIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
				rnd,
				amountIssuer.MaxAmount);

			var vsizeClient = new WabiSabiClient(
				vsizeIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
				rnd,
				vsizeIssuer.MaxAmount);

			var (amountZeroCredentials, vsizeZeroCredentials) = EnsureZeroCredentials(amountClient, vsizeClient, amountIssuer, vsizeIssuer);

			return (amountClient, vsizeClient, amountIssuer, vsizeIssuer, amountZeroCredentials, vsizeZeroCredentials);
		}

		private static (IEnumerable<Credential>, IEnumerable<Credential>) EnsureZeroCredentials(WabiSabiClient amClient, WabiSabiClient vsClient, CredentialIssuer amIssuer, CredentialIssuer vsIssuer)
		{
			var (zeroAmountCredentialRequest, amVal) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, vsVal) = vsClient.CreateRequestForZeroAmount();
			var amCredResp = amIssuer.HandleRequest(zeroAmountCredentialRequest);
			var vsCredResp = vsIssuer.HandleRequest(zeroVsizeCredentialRequest);

			return (
				amClient.HandleResponse(amCredResp, amVal),
				vsClient.HandleResponse(vsCredResp, vsVal));
		}

		public static (Alice alice, RealCredentialsRequest amountRequest, RealCredentialsRequest vsizeRequest) CreateRealCredentialRequests(Round round, Money? amount = null, long? vsize = null)
		{
			var (amClient, vsClient, _, _, amZeroCredentials, vsZeroCredentials) = CreateWabiSabiClientsAndIssuers(round);

			var alice = round.Alices.FirstOrDefault() ?? CreateAlice(round);
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				new[] { amount?.Satoshi ?? alice.CalculateRemainingAmountCredentials(round.FeeRate).Satoshi },
				amZeroCredentials,
				CancellationToken.None);
			var (realVsizeCredentialRequest, _) = vsClient.CreateRequest(
				new[] { vsize ?? alice.CalculateRemainingVsizeCredentials(round.MaxVsizeAllocationPerAlice) },
				vsZeroCredentials,
				CancellationToken.None);

			return (alice, realAmountCredentialRequest, realVsizeCredentialRequest);
		}

		public static ConnectionConfirmationRequest CreateConnectionConfirmationRequest(Round round)
		{
			var (alice, realAmountCredentialRequest, realVsizeCredentialRequest) = CreateRealCredentialRequests(round);

			var (amClient, vsClient, _, _, _, _) = CreateWabiSabiClientsAndIssuers(round);
			var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
			var (zeroVsizeCredentialRequest, _) = vsClient.CreateRequestForZeroAmount();

			return new ConnectionConfirmationRequest(
				round.Id,
				alice.Id,
				zeroAmountCredentialRequest,
				realAmountCredentialRequest,
				zeroVsizeCredentialRequest,
				realVsizeCredentialRequest);
		}

		public static OutputRegistrationRequest CreateOutputRegistrationRequest(Round round, Script? script = null, int? vsize = null)
		{
			var (amClient, vsClient, amIssuer, vsIssuer, amZeroCredentials, vsZeroCredentials) = CreateWabiSabiClientsAndIssuers(round);

			var alice = round.Alices.FirstOrDefault() ?? CreateAlice(round);
			var (amCredentialRequest, amValid) = amClient.CreateRequest(
				new[] { alice.CalculateRemainingAmountCredentials(round.FeeRate).Satoshi },
				amZeroCredentials, // FIXME doesn't make much sense
				CancellationToken.None);
			long startingVsizeCredentialAmount = vsize ?? alice.CalculateRemainingVsizeCredentials(round.MaxVsizeAllocationPerAlice);
			var (vsCredentialRequest, weValid) = vsClient.CreateRequest(
				new[] { startingVsizeCredentialAmount },
				vsZeroCredentials, // FIXME doesn't make much sense
				CancellationToken.None);

			var amResp = amIssuer.HandleRequest(amCredentialRequest);
			var weResp = vsIssuer.HandleRequest(vsCredentialRequest);
			var amountCredentials = amClient.HandleResponse(amResp, amValid);
			var vsizeCredentials = vsClient.HandleResponse(weResp, weValid);

			script ??= BitcoinFactory.CreateScript();
			var (realAmountCredentialRequest, _) = amClient.CreateRequest(
				amountCredentials,
				CancellationToken.None);

			var (realVsizeCredentialRequest, _) = vsClient.CreateRequest(
				vsizeCredentials,
				CancellationToken.None);

			return new OutputRegistrationRequest(
				round.Id,
				script,
				realAmountCredentialRequest,
				realVsizeCredentialRequest);
		}

		public static BlameRound CreateBlameRound(Round round, WabiSabiConfig cfg)
			=> new(new(cfg, round.Network, new InsecureRandom(), round.FeeRate), round, round.Alices.Select(x => x.Coin.Outpoint).ToHashSet());

		public static (Key, SmartCoin, Key, SmartCoin) CreateCoinKeyPairs()
		{
			var km = ServiceFactory.CreateKeyManager("");
			var smartCoin1 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m));
			var smartCoin2 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(2m));
			var sk1 = km.GetSecrets("", smartCoin1.ScriptPubKey).Single();
			var sk2 = km.GetSecrets("", smartCoin2.ScriptPubKey).Single();
			return (sk1.PrivateKey, smartCoin1, sk2.PrivateKey, smartCoin2);
		}
	}
}
