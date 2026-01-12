using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Coordinator;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;
using WalletWasabi.Coordinator.WabiSabi;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping;

public class StepConnectionConfirmationTests
{
	[Fact]
	public async Task AllConfirmedStepsAsync()
	{
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 4, MinInputCountByRoundMultiplier = 0.5 };
		var round = WabiSabiFactory.CreateRound(cfg);
		var a1 = WabiSabiFactory.CreateAlice(round);
		var a2 = WabiSabiFactory.CreateAlice(round);
		var a3 = WabiSabiFactory.CreateAlice(round);
		var a4 = WabiSabiFactory.CreateAlice(round);
		a1.ConfirmedConnection = true;
		a2.ConfirmedConnection = true;
		a3.ConfirmedConnection = true;
		a4.ConfirmedConnection = true;
		round.Alices.Add(a1);
		round.Alices.Add(a2);
		round.Alices.Add(a3);
		round.Alices.Add(a4);
		round.SetPhase(Phase.ConnectionConfirmation);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotAllConfirmedStaysAsync()
	{
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 4, MinInputCountByRoundMultiplier = 0.5 };
		var round = WabiSabiFactory.CreateRound(cfg);
		var a1 = WabiSabiFactory.CreateAlice(round);
		var a2 = WabiSabiFactory.CreateAlice(round);
		var a3 = WabiSabiFactory.CreateAlice(round);
		var a4 = WabiSabiFactory.CreateAlice(round);
		a1.ConfirmedConnection = true;
		a2.ConfirmedConnection = true;
		a3.ConfirmedConnection = true;
		a4.ConfirmedConnection = false;
		round.Alices.Add(a1);
		round.Alices.Add(a2);
		round.Alices.Add(a3);
		round.Alices.Add(a4);
		round.SetPhase(Phase.ConnectionConfirmation);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
		Assert.All(round.Alices, a => Assert.False(prison.IsBanned(a.Coin.Outpoint, cfg.GetDoSConfiguration(), DateTimeOffset.UtcNow)));

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task EnoughConfirmedTimedoutStepsAsync()
	{
		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		cfg.MaxInputCountByRound = 4;
		cfg.ConnectionConfirmationTimeout = TimeSpan.Zero;

		var round = WabiSabiFactory.CreateRound(cfg);
		var a1 = WabiSabiFactory.CreateAlice(round);
		var a2 = WabiSabiFactory.CreateAlice(round);
		var a3 = WabiSabiFactory.CreateAlice(round);
		var a4 = WabiSabiFactory.CreateAlice(round);
		a1.ConfirmedConnection = true;
		a2.ConfirmedConnection = true;
		a3.ConfirmedConnection = false;
		a4.ConfirmedConnection = false;
		round.Alices.Add(a1);
		round.Alices.Add(a2);
		round.Alices.Add(a3);
		round.Alices.Add(a4);
		round.SetPhase(Phase.ConnectionConfirmation);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		Assert.Equal(Phase.OutputRegistration, round.Phase);
		Assert.Equal(2, round.Alices.Count);
		var offendingAlices = new[] { a3, a4 };
		Assert.All(offendingAlices, alice => Assert.True(prison.IsBanned(alice.Coin.Outpoint, cfg.GetDoSConfiguration(), DateTimeOffset.UtcNow)));

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotEnoughConfirmedTimedoutDestroysAsync()
	{
		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		cfg.MaxInputCountByRound = 4;
		cfg.ConnectionConfirmationTimeout = TimeSpan.Zero;
		cfg.MinInputCountByRoundMultiplier = 0.9;

		var round = WabiSabiFactory.CreateRound(cfg);
		var a1 = WabiSabiFactory.CreateAlice(round);
		var a2 = WabiSabiFactory.CreateAlice(round);
		var a3 = WabiSabiFactory.CreateAlice(round);
		var a4 = WabiSabiFactory.CreateAlice(round);
		a1.ConfirmedConnection = true;
		a2.ConfirmedConnection = false;
		a3.ConfirmedConnection = false;
		a4.ConfirmedConnection = false;
		round.Alices.Add(a1);
		round.Alices.Add(a2);
		round.Alices.Add(a3);
		round.Alices.Add(a4);
		round.SetPhase(Phase.ConnectionConfirmation);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.GetActiveRounds());
		var offendingAlices = new[] { a2, a3, a4 };
		Assert.All(offendingAlices, alice => Assert.True(prison.IsBanned(alice.Coin.Outpoint, cfg.GetDoSConfiguration(), DateTimeOffset.UtcNow)));

		await arena.StopAsync(CancellationToken.None);
	}
}
