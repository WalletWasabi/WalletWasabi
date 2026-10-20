using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class RoundCreationTests
{
	private static Arena CreateArena(WabiSabiConfig cfg, IRPCClient rpc)
	{
		var arenaBuilder = ArenaBuilder.From(cfg).With(rpc);
		arenaBuilder.Period = TimeSpan.FromSeconds(1);
		return arenaBuilder.Create();
	}

	[Fact]
	public async Task InitializesRoundAsync()
	{
		WabiSabiConfig cfg = new();
		var mockRpc = BitcoinFactory.GetMockMinimalRpc();

		using Arena arena = CreateArena(cfg, mockRpc);
		Assert.Empty(arena.Rounds);
		await arena.StartAsync(TestContext.Current.CancellationToken);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(arena.Rounds);

		await arena.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task CreatesRoundIfNoneInputRegistrationAsync()
	{
		WabiSabiConfig cfg = new();
		var mockRpc = BitcoinFactory.GetMockMinimalRpc();

		using Arena arena = CreateArena(cfg, mockRpc);
		Assert.Empty(arena.Rounds);
		await arena.StartAsync(TestContext.Current.CancellationToken);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		var round = Assert.Single(arena.Rounds);

		round.SetPhase(Phase.ConnectionConfirmation);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(2, arena.Rounds.Count);

		await arena.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task CreatesRoundIfInBlameInputRegistrationAsync()
	{
		WabiSabiConfig cfg = new();
		var mockRpc = BitcoinFactory.GetMockMinimalRpc();

		using Arena arena = CreateArena(cfg, mockRpc);
		Assert.Empty(arena.Rounds);
		await arena.StartAsync(TestContext.Current.CancellationToken);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		var round = Assert.Single(arena.Rounds);

		round.SetPhase(Phase.ConnectionConfirmation);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		Assert.Equal(Phase.InputRegistration, blameRound.Phase);
		arena.Rounds.Add(blameRound);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(3, arena.Rounds.Count);
		Assert.Equal(2, arena.Rounds.Count(x => x.Phase == Phase.InputRegistration));

		await arena.StopAsync(TestContext.Current.CancellationToken);
	}
}
