using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Coordinator;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.UnitTests;
using WalletWasabi.Tests.UnitTests.WabiSabi;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.Coordinator.WabiSabi;

namespace WalletWasabi.Tests.Helpers;

public static class WabiSabiFactory
{
	private static string CoordinatorIdentifier = new WabiSabiConfig().CoordinatorIdentifier;

	public static Coin CreateCoin(Key? key = null, Money? amount = null, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		key ??= new();
		amount ??= Money.Coins(1);
		return new(
			new OutPoint(Hashes.DoubleSHA256(key.PubKey.ToBytes().Concat(BitConverter.GetBytes(amount)).ToArray()), 0),
			new TxOut(amount, key.PubKey.GetScriptPubKey(scriptPubKeyType)));
	}

	public static Tuple<Coin, OwnershipProof> CreateCoinWithOwnershipProof(Key? key = null, Money? amount = null, uint256? roundId = null, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		key ??= new();
		var coin = CreateCoin(key, amount, scriptPubKeyType);
		roundId ??= uint256.One;
		var ownershipProof = CreateOwnershipProof(key, roundId);
		return new Tuple<Coin, OwnershipProof>(coin, ownershipProof);
	}

	public static CoinJoinInputCommitmentData CreateCommitmentData(uint256? roundId = null)
		=> new(CoordinatorIdentifier, roundId ?? uint256.One);

	public static OwnershipProof CreateOwnershipProof(Key key, uint256? roundHash = null, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
		=> OwnershipProof.GenerateCoinJoinInputProof(
			key,
			GetOwnershipIdentifier(key.PubKey.GetScriptPubKey(scriptPubKeyType)),
			new CoinJoinInputCommitmentData(CoordinatorIdentifier, roundHash ?? BitcoinFactory.CreateUint256()),
			scriptPubKeyType);

	public static OwnershipIdentifier GetOwnershipIdentifier(Script scriptPubKey)
	{
		using var identificationKey = Key.Parse("5KbdaBwc9Eit2LrmDp1WfZd815StNstwHanbRrPpGGN6wWJKyHe", Network.Main);
		return new OwnershipIdentifier(identificationKey, scriptPubKey);
	}

	public static RoundParameters CreateRoundParameters(WabiSabiConfig cfg) =>
		RoundParameters.Create(
			cfg,
			new FeeRate(100m),
			Money.Coins(Constants.MaximumNumberOfBitcoins));

	public static Round CreateRound(RoundParameters parameters) =>
		new(parameters, InsecureRandom.Instance);

	public static Round CreateRound(WabiSabiConfig cfg) =>
		CreateRound(CreateRoundParameters(cfg) with
		{
			MaxVsizeAllocationPerAlice =
				Constants.P2wpkhInputVirtualSize + Constants.P2wpkhOutputVirtualSize // enough vsize for one input and one output
		});

	public static MockRpcClient CreatePreconfiguredRpcClient(params Coin[] coins)
	{
		using Key key = new();
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (txId, n, _) =>
		{
			var maybeCoin = coins.FirstOrDefault(x => x.Outpoint.Hash == txId && x.Outpoint.N == n);
			if (maybeCoin is { } coin)
			{
				return new GetTxOutResponse
				{
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					Confirmations = 120,
					TxOut = coin.TxOut,
				};
			}
			return new GetTxOutResponse
			{
				IsCoinBase = false,
				ScriptPubKeyType = "witness_v0_keyhash",
				Confirmations = 120,
				TxOut = new TxOut(Money.Coins(1), BitcoinFactory.CreateScript()),
			};
		};
		mockRpc.OnGetRawTransactionAsync = (_, _) =>
			Task.FromResult(BitcoinFactory.CreateTransaction());

		mockRpc.OnEstimateSmartFeeAsync = (_, _) =>
			Task.FromResult(new EstimateSmartFeeResponse
			{
				Blocks = 1000,
				FeeRate = new FeeRate(10m)
			});
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MinRelayTxFee = 1
			});
		mockRpc.OnGetBlockCountAsync = () => Task.FromResult(600);
		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(500));
		return mockRpc;
	}

	public static Alice CreateAlice(Coin coin, OwnershipProof ownershipProof, Round round)
		=> new(coin, ownershipProof, round, Guid.NewGuid()) { Deadline = DateTimeOffset.UtcNow + TimeSpan.FromHours(1) };

	public static Alice CreateAlice(Key key, Money amount, Round round, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
		=> CreateAlice(CreateCoin(key, amount, scriptPubKeyType), CreateOwnershipProof(key, round.Id, scriptPubKeyType), round);

	public static Alice CreateAlice(Money amount, Round round)
	{
		using var key = new Key();
		return CreateAlice(key, amount, round);
	}

	public static Alice CreateAlice(Key key, Round round, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
		=> CreateAlice(key, Money.Coins(1), round, scriptPubKeyType);

	public static Alice CreateAlice(Round round)
	{
		using var key = new Key();
		return CreateAlice(key, Money.Coins(1), round);
	}

	public static ArenaClient CreateArenaClient(Arena arena, Round? round = null)
	{
		var roundState = RoundState.FromRound(round ?? arena.Rounds.First());
		return new ArenaClient(
			roundState.CreateAmountCredentialClient(InsecureRandom.Instance),
			roundState.CreateVsizeCredentialClient(InsecureRandom.Instance),
			CoordinatorIdentifier,
			arena);
	}

	public static InputRegistrationRequest CreateInputRegistrationRequest(Round round, Key? key = null, OutPoint? prevout = null)
	{
		(var amClient, var vsClient, _, _, _, _) = CreateWabiSabiClientsAndIssuers(round);
		var (zeroAmountCredentialRequest, _) = amClient.CreateRequestForZeroAmount();
		var (zeroVsizeCredentialRequest, _) = vsClient.CreateRequestForZeroAmount();

		using var newkey = new Key();
		return new(
			round.Id,
			prevout ?? BitcoinFactory.CreateOutPoint(),
			CreateOwnershipProof(key ?? newkey, round.Id),
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
		var amountIssuer = round.AmountCredentialIssuer;
		var vsizeIssuer = round.VsizeCredentialIssuer;
		var amountClient = new WabiSabiClient(
			amountIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
			InsecureRandom.Instance,
			amountIssuer.MaxAmount);

		var vsizeClient = new WabiSabiClient(
			vsizeIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters(),
			InsecureRandom.Instance,
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
			new[] { amount?.Satoshi ?? alice.CalculateRemainingAmountCredentials(round.Parameters.MiningFeeRate).Satoshi },
			amZeroCredentials,
			CancellationToken.None);
		var (realVsizeCredentialRequest, _) = vsClient.CreateRequest(
			new[] { vsize ?? alice.CalculateRemainingVsizeCredentials(round.Parameters.MaxVsizeAllocationPerAlice) },
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
			new[] { alice.CalculateRemainingAmountCredentials(round.Parameters.MiningFeeRate).Satoshi },
			amZeroCredentials, // FIXME doesn't make much sense
			CancellationToken.None);
		long startingVsizeCredentialAmount = vsize ?? alice.CalculateRemainingVsizeCredentials(round.Parameters.MaxVsizeAllocationPerAlice);
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
	{
		var roundParameters = RoundParameters.Create(
				cfg,
				round.Parameters.MiningFeeRate,
				round.Parameters.MaxSuggestedAmount) with
		{
			MinInputCountByRound = cfg.MinInputCountByBlameRound
		};

		return new BlameRound(
			parameters: roundParameters,
			blameOf: round,
			blameWhitelist: round.Alices.Select(x => x.Coin.Outpoint).ToHashSet(),
			InsecureRandom.Instance);
	}

	public static (IKeyChain, SmartCoin, SmartCoin) CreateCoinKeyPairs(KeyManager? keyManager = null)
	{
		var km = keyManager ?? ServiceFactory.CreateKeyManager("");
		var keyChain = new KeyChain(km, "");

		var smartCoin1 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m));
		var smartCoin2 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(2m));
		return (keyChain, smartCoin1, smartCoin2);
	}

	public static CoinJoinClient CreateTestCoinJoinClient(
		Func<string, IWabiSabiApiRequestHandler> apiClientFactory,
		KeyManager keyManager,
		RoundStateProvider roundStateProvider)
	{
		return CreateTestCoinJoinClient(
			apiClientFactory,
			new KeyChain(keyManager, ""),
			new OutputProvider(new InternalDestinationProvider(keyManager)),
			roundStateProvider,
			keyManager.NonPrivateCoinIsolation);
	}

	public static CoinJoinClient CreateTestCoinJoinClient(
		Func<string, IWabiSabiApiRequestHandler> apiClientFactory,
		IKeyChain keyChain,
		OutputProvider outputProvider,
		RoundStateProvider roundStateProvider,
		bool redCoinIsolation)
	{
		var semiPrivateThreshold = redCoinIsolation ? Constants.SemiPrivateThreshold : 0;
		var coinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: int.MaxValue, semiPrivateThreshold: semiPrivateThreshold);
		var coinjoinClient = new TesteableCoinJoinClient(
			apiClientFactory,
			keyChain,
			outputProvider,
			roundStateProvider,
			coinSelector,
			new CoinJoinConfiguration("CoinJoinCoordinatorIdentifier", 150.0m, 1, AllowSoloCoinjoining: true),
			new LiquidityClueProvider(),
			TimeSpan.Zero);

		return coinjoinClient;
	}

	public static RoundParameterFactory CreateRoundParametersFactory(WabiSabiConfig cfg, int maxVsizeAllocationPerAlice) =>
		(rate, maxSuggestedAmount) => CreateRoundParameters(cfg) with
			{
				MinInputCountByRound = cfg.MinInputCountByBlameRound,
				MaxSuggestedAmount = maxSuggestedAmount,
				MaxVsizeAllocationPerAlice = maxVsizeAllocationPerAlice
			};

	public static (Prison, ChannelReader<Offender>) CreateObservablePrison()
	{
		var channel = Channel.CreateUnbounded<Offender>();
		var prison = new Prison(
			Enumerable.Empty<Offender>(),
			channel.Writer);
		return (prison, channel.Reader);
	}

	public static Prison CreatePrison()
	{
		var (prison, _) = CreateObservablePrison();
		return prison;
	}

	internal static WabiSabiConfig CreateWabiSabiConfig()
	{
		return new WabiSabiConfig
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
			MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice),

			DoSSeverity = Money.Coins(1.0m),
			DoSMinTimeForFailedToVerify = TimeSpan.FromDays(30),
			DoSMinTimeForCheating = TimeSpan.FromDays(1),
			DoSMinTimeInPrison = TimeSpan.FromHours(1),
			DoSPenaltyFactorForDisruptingConfirmation = 1.0d,
			DoSPenaltyFactorForDisruptingSignalReadyToSign = 1.5d,
			DoSPenaltyFactorForDisruptingSigning = 1.5d,
			DoSPenaltyFactorForDisruptingByDoubleSpending = 3.0d
		};
	}
}

public class TesteableCoinJoinClient : CoinJoinClient
{
	public TesteableCoinJoinClient(Func<string, IWabiSabiApiRequestHandler> arenaRequestHandlerFactory, IKeyChain keyChain, OutputProvider outputProvider, RoundStateProvider roundStatusProvider, CoinJoinCoinSelector coinJoinCoinSelector, CoinJoinConfiguration coinJoinConfiguration, LiquidityClueProvider liquidityClueProvider,  TimeSpan doNotRegisterInLastMinuteTimeLimit = default) : base(arenaRequestHandlerFactory, keyChain, outputProvider, roundStatusProvider, coinJoinCoinSelector, coinJoinConfiguration, liquidityClueProvider, doNotRegisterInLastMinuteTimeLimit)
	{
	}

	internal override ImmutableList<DateTimeOffset> GetScheduledDates(int howMany, DateTimeOffset startTime, DateTimeOffset endTime,
		TimeSpan maximumRequestDelay)
	{
		return base.GetScheduledDates(howMany, startTime, endTime, TimeSpan.FromSeconds(1));
	}
}
