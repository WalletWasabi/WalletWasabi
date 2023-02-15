using Moq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tests.Helpers;

public static class WabiSabiFactory
{
	public static Coin CreateCoin(Key? key = null, Money? amount = null, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		key ??= new();
		amount ??= Money.Coins(1);
		return new(
			new OutPoint(Hashes.DoubleSHA256(key.PubKey.ToBytes().Concat(BitConverter.GetBytes(amount)).ToArray() ), 0),
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
			Network.Main,
			new FeeRate(100m),
			cfg.CoordinationFeeRate,
			Money.Coins(Constants.MaximumNumberOfBitcoins));

	public static Round CreateRound(RoundParameters parameters) =>
		new(parameters, InsecureRandom.Instance);

	public static Round CreateRound(WabiSabiConfig cfg) =>
		CreateRound(CreateRoundParameters(cfg) with
		{
			MaxVsizeAllocationPerAlice = 11 + 31 + MultipartyTransactionParameters.SharedOverhead
		});

	public static Mock<IRPCClient> CreatePreconfiguredRpcClient(params Coin[] coins)
	{
		using Key key = new();
		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetTxOutAsync(It.IsAny<uint256>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new GetTxOutResponse
			{
				IsCoinBase = false,
				ScriptPubKeyType = "witness_v0_keyhash",
				Confirmations = 120,
				TxOut = new TxOut(Money.Coins(1), BitcoinFactory.CreateScript()),
			});
		foreach (var coin in coins)
		{
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(coin.Outpoint.Hash, (int)coin.Outpoint.N, true, It.IsAny<CancellationToken>()))
				.ReturnsAsync(new GetTxOutResponse
				{
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					Confirmations = 120,
					TxOut = coin.TxOut,
				});

			mockRpc.Setup(rpc => rpc.GetRawTransactionAsync(coin.Outpoint.Hash, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(BitcoinFactory.CreateTransaction());
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

	public static Alice CreateAlice(Coin coin, OwnershipProof ownershipProof, Round round)
		=> new(coin, ownershipProof, round, Guid.NewGuid(), false) { Deadline = DateTimeOffset.UtcNow + TimeSpan.FromHours(1) };

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
			new[] { amount?.Satoshi ?? alice.CalculateRemainingAmountCredentials(round.Parameters.MiningFeeRate, round.Parameters.CoordinationFeeRate).Satoshi },
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
			new[] { alice.CalculateRemainingAmountCredentials(round.Parameters.MiningFeeRate, round.Parameters.CoordinationFeeRate).Satoshi },
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
		=> new(RoundParameters.Create(cfg, round.Parameters.Network, round.Parameters.MiningFeeRate, round.Parameters.CoordinationFeeRate, round.Parameters.MaxSuggestedAmount),
			blameOf: round,
			blameWhitelist: round.Alices.Select(x => x.Coin.Outpoint).ToHashSet(),
			InsecureRandom.Instance);

	public static (IKeyChain, SmartCoin, SmartCoin) CreateCoinKeyPairs(KeyManager? keyManager = null)
	{
		var km = keyManager ?? ServiceFactory.CreateKeyManager("");
		var keyChain = new KeyChain(km, new Kitchen(""));

		var smartCoin1 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m));
		var smartCoin2 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(2m));
		return (keyChain, smartCoin1, smartCoin2);
	}

	public static CoinJoinClient CreateTestCoinJoinClient(
		IWasabiHttpClientFactory httpClientFactory,
		KeyManager keyManager,
		RoundStateUpdater roundStateUpdater)
	{
		return CreateTestCoinJoinClient(
			httpClientFactory,
			new KeyChain(keyManager, new Kitchen("")),
			new InternalDestinationProvider(keyManager),
			roundStateUpdater,
			keyManager.RedCoinIsolation);
	}

	public static CoinJoinClient CreateTestCoinJoinClient(
		IWasabiHttpClientFactory httpClientFactory,
		IKeyChain keyChain,
		IDestinationProvider destinationProvider,
		RoundStateUpdater roundStateUpdater,
		bool redCoinIsolation)
	{
		var mock = new Mock<CoinJoinClient>(
			httpClientFactory,
			keyChain,
			destinationProvider,
			roundStateUpdater,
			"CoinJoinCoordinatorIdentifier",
			new LiquidityClueProvider(),
			int.MaxValue,
			true,
			redCoinIsolation,
			TimeSpan.Zero,
			TimeSpan.Zero);

		// Overwrite Maximum Request Delay parameter but still use the original method.
		mock.Setup(m => m.GetScheduledDates(It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsNotIn(TimeSpan.FromSeconds(1))))
			.Returns((int howMany, DateTimeOffset endTime, TimeSpan maximumRequestDelay) => mock.Object.GetScheduledDates(howMany, endTime, TimeSpan.FromSeconds(1)));

		mock.CallBase = true;

		return mock.Object;
	}

	public static RoundParameterFactory CreateRoundParametersFactory(WabiSabiConfig cfg, Network network, int maxVsizeAllocationPerAlice)
	{
		var mockRoundParameterFactory = new Mock<RoundParameterFactory>(cfg, network);
		mockRoundParameterFactory.Setup(x => x.CreateRoundParameter(It.IsAny<FeeRate>(), It.IsAny<Money>()))
			.Returns(WabiSabiFactory.CreateRoundParameters(cfg)
				with
			{
				MaxVsizeAllocationPerAlice = maxVsizeAllocationPerAlice
			});
		mockRoundParameterFactory.Setup(x => x.CreateBlameRoundParameter(It.IsAny<FeeRate>(), It.IsAny<Round>()))
			.Returns(WabiSabiFactory.CreateRoundParameters(cfg)
				with
			{
				MaxVsizeAllocationPerAlice = maxVsizeAllocationPerAlice
			});
		return mockRoundParameterFactory.Object;
	}

	private static string CoordinatorIdentifier = new WabiSabiConfig().CoordinatorIdentifier;
}
