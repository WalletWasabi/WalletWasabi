using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

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

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
		Assert.Equal(0, prison.CountInmates().noted);
		Assert.Equal(0, prison.CountInmates().banned);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task EnoughConfirmedTimedoutStepsAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5,
			ConnectionConfirmationTimeout = TimeSpan.Zero
		};
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

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.Ended, round.Phase);
		//Assert.Equal(Phase.OutputRegistration, round.Phase);
		Assert.Equal(2, round.Alices.Count);
		Assert.Equal(2, prison.CountInmates().noted);
		Assert.Equal(0, prison.CountInmates().banned);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotEnoughConfirmedTimedoutDestroysAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5,
			ConnectionConfirmationTimeout = TimeSpan.Zero
		};
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

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, prison).CreateAndStartAsync(round);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.GetActiveRounds());
		Assert.Equal(3, prison.CountInmates().noted);
		Assert.Equal(0, prison.CountInmates().banned);

		await arena.StopAsync(CancellationToken.None);
	}
}
