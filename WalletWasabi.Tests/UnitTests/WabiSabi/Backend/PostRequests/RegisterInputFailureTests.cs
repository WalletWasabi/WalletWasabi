using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;
using Arena = WalletWasabi.WabiSabi.Coordinator.Rounds.Arena;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterInputFailureTests
{
	private static async Task RegisterAndAssertWrongPhaseAsync(InputRegistrationRequest req, Arena handler)
	{
		var ex = await Assert.ThrowsAsync<WrongPhaseException>(async () => await handler.RegisterInputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
	}

	[Fact]
	public async Task RoundNotFoundAsync()
	{
		using Key key = new();
		using Arena arena = await ArenaBuilder.Default.CreateAndStartAsync();
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(uint256.Zero, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongPhaseAsync()
	{
		WabiSabiConfig cfg = new();
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync();
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		var round = arena.Rounds.First();
		var req = WabiSabiFactory.CreateInputRegistrationRequest(round, key: key, coin.Outpoint);

		foreach (Phase phase in Enum.GetValues(typeof(Phase)))
		{
			if (phase != Phase.InputRegistration)
			{
				round.SetPhase(phase);
				await RegisterAndAssertWrongPhaseAsync(req, arena);
			}
		}

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationFullAsync()
	{
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
		var round = WabiSabiFactory.CreateRound(cfg);
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);

		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WrongPhaseException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
		Assert.Equal(Phase.InputRegistration, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimedOutAsync()
	{
		WabiSabiConfig cfg = new() { StandardInputRegistrationTimeout = TimeSpan.Zero };
		var round = WabiSabiFactory.CreateRound(cfg);
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync();

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		arena.Rounds.Add(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WrongPhaseException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
		Assert.Equal(Phase.InputRegistration, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputBannedAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);

		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		var round = WabiSabiFactory.CreateRound(cfg);

		Prison prison = WabiSabiFactory.CreatePrison();
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg, rpc, prison).CreateAndStartAsync(round);
		prison.FailedToSign(coin.Outpoint, Money.Coins(1m), round.Id);

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		Assert.IsAssignableFrom<InputBannedExceptionData>(ex.ExceptionData);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputLongBannedAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);

		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		Prison prison = WabiSabiFactory.CreatePrison();
		prison.FailedVerification(coin.Outpoint, round.Id);
		using Arena arena = await ArenaBuilder.From(cfg, rpc, prison).CreateAndStartAsync(round);

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		Assert.IsAssignableFrom<InputBannedExceptionData>(ex.ExceptionData);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputCantBeNotedAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);

		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		cfg.AllowNotedInputRegistration = false;

		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		Prison prison = WabiSabiFactory.CreatePrison();
		prison.FailedToConfirm(coin.Outpoint, Money.Coins(1m), round.Id);

		using Arena arena = await ArenaBuilder.From(cfg, rpc, prison).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputSpentAsync()
	{
		using Key key = new();
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (_, _, _) => null;

		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync(round);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputUnconfirmedAsync()
	{
		using Key key = new();
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (_, _, _) =>
			new NBitcoin.RPC.GetTxOutResponse { Confirmations = 0 };

		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync(round);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.InputUnconfirmed, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputImmatureAsync()
	{
		using Key key = new();
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient();
		var callCounter = 1;
		rpc.OnGetTxOutAsync = (_, _, _) =>
		{
			var ret = new NBitcoin.RPC.GetTxOutResponse { Confirmations = callCounter, IsCoinBase = true };
			callCounter++;
			return ret;
		};
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var req = WabiSabiFactory.CreateInputRegistrationRequest(round: round);
		foreach (var i in Enumerable.Range(1, 100))
		{
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
				async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));

			Assert.Equal(WabiSabiProtocolErrorCode.InputImmature, ex.ErrorCode);
		}

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootNotAllowedAsync()
	{
		WabiSabiConfig cfg = new() { AllowP2trInputs = false };
		var round = WabiSabiFactory.CreateRound(cfg);

		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key, scriptPubKeyType: ScriptPubKeyType.TaprootBIP86);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + (cfg.ConnectionConfirmationTimeout * 0.9);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id, scriptPubKeyType: ScriptPubKeyType.TaprootBIP86);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongScriptPubKeyInOwnershipProofAsync()
	{
		await TestOwnershipProofAsync((key, roundId) => WabiSabiFactory.CreateOwnershipProof(new Key(), roundId));
	}

	[Fact]
	public async Task WrongRoundIdInOwnershipProofAsync()
	{
		await TestOwnershipProofAsync((key, roundId) => WabiSabiFactory.CreateOwnershipProof(key, uint256.One));
	}

	[Fact]
	public async Task WrongCoordinatorIdentifierInOwnershipProofAsync()
	{
		await TestOwnershipProofAsync((key, roundId) => OwnershipProof.GenerateCoinJoinInputProof(key, new OwnershipIdentifier(key, key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)), new CoinJoinInputCommitmentData("test", roundId), ScriptPubKeyType.Segwit));
	}

	[Fact]
	public async Task NotEnoughFundsAsync()
	{
		using Key key = new();
		var txOut = new TxOut(Money.Coins(1.0m), key.GetScriptPubKey(ScriptPubKeyType.Segwit));
		var outpoint = BitcoinFactory.CreateOutPoint();
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(new Coin(outpoint, txOut));

		WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TooMuchFundsAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(0.9m) };
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TooMuchVsizeAsync()
	{
		// Configures a round that allows so many inputs (Alices) that
		// the virtual size each of they have available is not enough
		// to register anything.
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 100_000 };
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);

		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		RoundParameters RoundParameterFactory(FeeRate rate, Money amount) => RoundParameters.Create(cfg, rate, amount) with {MaxVsizeAllocationPerAlice = 0};
		Round round = WabiSabiFactory.CreateRound(RoundParameterFactory(new FeeRate(10m), Money.Zero));
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).With((RoundParameterFactory) RoundParameterFactory).CreateAndStartAsync(round);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.TooMuchVsize, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceAlreadyRegisteredIntraRoundAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		// Make sure an Alice have already been registered with the same input.
		var anotherAlice = WabiSabiFactory.CreateAlice(coin, WabiSabiFactory.CreateOwnershipProof(key), round);
		round.Alices.Add(anotherAlice);
		round.SetPhase(Phase.ConnectionConfirmation);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.AliceAlreadyRegistered, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceAlreadyRegisteredCrossRoundAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var anotherRound = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		// Make sure an Alice have already been registered with the same input.
		var preAlice = WabiSabiFactory.CreateAlice(coin, WabiSabiFactory.CreateOwnershipProof(key), round);
		anotherRound.Alices.Add(preAlice);
		anotherRound.SetPhase(Phase.ConnectionConfirmation);

		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round, anotherRound);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.AliceAlreadyRegistered, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	private async Task TestOwnershipProofAsync(Func<Key, uint256, OwnershipProof> ownershipProofFunc)
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		OwnershipProof ownershipProof = ownershipProofFunc(key, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.WrongOwnershipProof, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}
}
