using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class StepInputRegistrationTests
	{
		[Fact]
		public async Task RoundFullAsync()
		{
			WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			round.Alices.Add(WabiSabiFactory.CreateAlice());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.InputRegistration, round.Phase);

			round.Alices.Add(WabiSabiFactory.CreateAlice());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.InputRegistration, round.Phase);

			round.Alices.Add(WabiSabiFactory.CreateAlice());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundFullAliceMultiInputAsync()
		{
			WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			round.Alices.Add(WabiSabiFactory.CreateAlice(WabiSabiFactory.CreateInputRoundSignaturePairs(2)));
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.InputRegistration, round.Phase);

			round.Alices.Add(WabiSabiFactory.CreateAlice());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputRegistrationTimedoutWithSufficientInputsAsync()
		{
			WabiSabiConfig cfg = new()
			{
				InputRegistrationTimeout = TimeSpan.FromHours(1),
				MaxInputCountByRound = 4,
				MinInputCountByRoundMultiplier = 0.5
			};
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			round.Alices.Add(WabiSabiFactory.CreateAlice());
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			cfg.InputRegistrationTimeout = TimeSpan.Zero;
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputRegistrationTimedoutWithoutSufficientInputsAsync()
		{
			WabiSabiConfig cfg = new()
			{
				InputRegistrationTimeout = TimeSpan.Zero,
				MaxInputCountByRound = 4,
				MinInputCountByRoundMultiplier = 0.5
			};
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			round.Alices.Add(WabiSabiFactory.CreateAlice());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.InputRegistration, round.Phase);
			Assert.DoesNotContain(round.Id, arena.Rounds.Keys);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
