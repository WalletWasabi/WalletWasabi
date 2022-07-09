using Moq;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

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
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync();
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		var round = arena.Rounds.First();
		using Key key = new();
		var req = WabiSabiFactory.CreateInputRegistrationRequest(round, key: key);

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
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WrongPhaseException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
		Assert.Equal(Phase.InputRegistration, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimedoutAsync()
	{
		WabiSabiConfig cfg = new() { StandardInputRegistrationTimeout = TimeSpan.Zero };
		var round = WabiSabiFactory.CreateRound(cfg);
		using Key key = new();
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
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
		var outpoint = BitcoinFactory.CreateOutPoint();

		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		prison.Punish(outpoint, Punishment.Banned, uint256.One);

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		Assert.IsAssignableFrom<InputBannedExceptionData>(ex.ExceptionData);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputLongBannedAsync()
	{
		using Key key = new();
		var outpoint = BitcoinFactory.CreateOutPoint();

		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		prison.Punish(outpoint, Punishment.LongBanned, uint256.One);

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.InputLongBanned, ex.ErrorCode);
		Assert.IsAssignableFrom<InputBannedExceptionData>(ex.ExceptionData);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputCanBeNotedAsync()
	{
		using Key key = new();
		var outpoint = BitcoinFactory.CreateOutPoint();

		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		prison.Punish(outpoint, Punishment.Noted, uint256.One);

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None));
		Assert.NotEqual(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputCantBeNotedAsync()
	{
		using Key key = new();
		var outpoint = BitcoinFactory.CreateOutPoint();

		WabiSabiConfig cfg = new() { AllowNotedInputRegistration = false };
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		prison.Punish(outpoint, Punishment.Noted, uint256.One);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None));
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

		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetTxOutAsync(It.IsAny<uint256>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((NBitcoin.RPC.GetTxOutResponse?)null);

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

		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetTxOutAsync(It.IsAny<uint256>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse { Confirmations = 0 });

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
		var rpcCfg = rpc.SetupSequence(rpc => rpc.GetTxOutAsync(It.IsAny<uint256>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
		foreach (var i in Enumerable.Range(1, 100))
		{
			rpcCfg = rpcCfg.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse { Confirmations = i, IsCoinBase = true });
		}
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
	public async Task ScriptNotAllowedAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		using Key key = new();
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetTxOutAsync(It.IsAny<uint256>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
			{
				Confirmations = 1,
				TxOut = new(Money.Coins(1), key.PubKey.ScriptPubKey.Hash.GetAddress(Network.Main))
			});

		var prison = new Prison();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);
		Assert.Single(prison.GetInmates());
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongOwnershipProofAsync()
	{
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		using Key nonOwnerKey = new();
		var wrongOwnershipProof = WabiSabiFactory.CreateOwnershipProof(nonOwnerKey, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, wrongOwnershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.WrongOwnershipProof, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotEnoughFundsAsync()
	{
		using Key key = new();
		WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TooMuchFundsAsync()
	{
		using Key key = new();
		WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(0.9m) };
		var round = WabiSabiFactory.CreateRound(cfg);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arenaClient.RegisterInputAsync(round.Id, BitcoinFactory.CreateOutPoint(), ownershipProof, CancellationToken.None));
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
		RoundParameterFactory roundParameterFactory = WabiSabiFactory.CreateRoundParametersFactory(cfg, rpc.Object.Network, maxVsizeAllocationPerAlice: 0);
		Round round = WabiSabiFactory.CreateRound(roundParameterFactory.CreateRoundParameter(new FeeRate(10m), Money.Zero));
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).With(roundParameterFactory).CreateAndStartAsync(round);
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
}
