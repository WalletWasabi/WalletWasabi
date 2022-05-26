using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin.RPC;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping;

public class StepInputRegistrationTests
{
	[Fact]
	public async Task RoundFullAsync()
	{
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
		var round = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DetectSpentTxoBeforeSteppingIntoConnectionConfirmationAsync()
	{
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
		var round = WabiSabiFactory.CreateRound(cfg);
		var offendingAlice = WabiSabiFactory.CreateAlice(round); // this Alice spent the coin after registration

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient();
		mockRpc.Setup(rpc => rpc.GetTxOutAsync(offendingAlice.Coin.Outpoint.Hash, (int)offendingAlice.Coin.Outpoint.N, true, It.IsAny<CancellationToken>()))
			.ReturnsAsync((GetTxOutResponse?)null);

		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(offendingAlice);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);
		Assert.Equal(2, round.Alices.Count); // the offending alice was removed

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
		Assert.Equal(3, round.Alices.Count);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task BlameRoundFullAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5
		};
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice1 = WabiSabiFactory.CreateAlice(round);
		var alice2 = WabiSabiFactory.CreateAlice(round);
		var alice3 = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice1);
		round.Alices.Add(alice2);
		round.Alices.Add(alice3);
		var blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync( blameRound);

		blameRound.Alices.Add(alice1);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, blameRound.Phase);

		blameRound.Alices.Add(alice2);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, blameRound.Phase);

		blameRound.Alices.Add(alice3);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, blameRound.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimedoutWithSufficientInputsAsync()
	{
		WabiSabiConfig cfg = new()
		{
			StandardInputRegistrationTimeout = TimeSpan.Zero,
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5
		};
		var round = WabiSabiFactory.CreateRound(cfg);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task BlameRoundTimedoutWithSufficientInputsAsync()
	{
		WabiSabiConfig cfg = new()
		{
			BlameInputRegistrationTimeout = TimeSpan.Zero,
			StandardInputRegistrationTimeout = TimeSpan.FromHours(1), // Test that this is disregarded.
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5
		};
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice1 = WabiSabiFactory.CreateAlice(round);
		var alice2 = WabiSabiFactory.CreateAlice(round);
		var alice3 = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice1);
		round.Alices.Add(alice2);
		round.Alices.Add(alice3);
		var blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		blameRound.Alices.Add(alice1);
		blameRound.Alices.Add(alice2);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync( blameRound);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, blameRound.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimedoutWithoutSufficientInputsAsync()
	{
		WabiSabiConfig cfg = new()
		{
			StandardInputRegistrationTimeout = TimeSpan.Zero,
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5
		};
		var round = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.DoesNotContain(round, arena.GetActiveRounds());

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task BlameRoundTimedoutWithoutSufficientInputsAsync()
	{
		// This test also tests that the min input count multiplier is applied
		// against the max input count by round number and not against the
		// number of inputs awaited by the blame round itself.
		WabiSabiConfig cfg = new()
		{
			BlameInputRegistrationTimeout = TimeSpan.Zero,
			StandardInputRegistrationTimeout = TimeSpan.FromHours(1), // Test that this is disregarded.
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5
		};
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice1 = WabiSabiFactory.CreateAlice(round);
		var alice2 = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice1);
		round.Alices.Add(alice2);
		var blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		blameRound.Alices.Add(alice1);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync( blameRound);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.Ended, blameRound.Phase);
		Assert.DoesNotContain(blameRound, arena.GetActiveRounds());

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimeoutCanBeModifiedRuntimeAsync()
	{
		WabiSabiConfig cfg = new()
		{
			StandardInputRegistrationTimeout = TimeSpan.FromHours(1),
			MaxInputCountByRound = 4,
			MinInputCountByRoundMultiplier = 0.5
		};
		var round = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);

		round.InputRegistrationTimeFrame = round.InputRegistrationTimeFrame with { Duration = TimeSpan.Zero };

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}
}
