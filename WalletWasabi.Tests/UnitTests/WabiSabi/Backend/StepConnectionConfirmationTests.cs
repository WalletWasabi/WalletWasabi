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
	public class StepConnectionConfirmation
	{
		[Fact]
		public async Task AllConfirmedStepsAsync()
		{
			WabiSabiConfig cfg = new() { MaxInputCountByRound = 4, MinInputCountByRoundMultiplier = 0.5 };
			var round = WabiSabiFactory.CreateRound(cfg);
			var a1 = WabiSabiFactory.CreateAlice();
			var a2 = WabiSabiFactory.CreateAlice();
			var a3 = WabiSabiFactory.CreateAlice();
			var a4 = WabiSabiFactory.CreateAlice();
			a1.ConfirmedConnetion = true;
			a2.ConfirmedConnetion = true;
			a3.ConfirmedConnetion = true;
			a4.ConfirmedConnetion = true;
			round.Alices.Add(a1);
			round.Alices.Add(a2);
			round.Alices.Add(a3);
			round.Alices.Add(a4);
			round.SetPhase(Phase.ConnectionConfirmation);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NotAllConfirmedStaysAsync()
		{
			WabiSabiConfig cfg = new() { MaxInputCountByRound = 4, MinInputCountByRoundMultiplier = 0.5 };
			var round = WabiSabiFactory.CreateRound(cfg);
			var a1 = WabiSabiFactory.CreateAlice();
			var a2 = WabiSabiFactory.CreateAlice();
			var a3 = WabiSabiFactory.CreateAlice();
			var a4 = WabiSabiFactory.CreateAlice();
			a1.ConfirmedConnetion = true;
			a2.ConfirmedConnetion = true;
			a3.ConfirmedConnetion = true;
			a4.ConfirmedConnetion = false;
			round.Alices.Add(a1);
			round.Alices.Add(a2);
			round.Alices.Add(a3);
			round.Alices.Add(a4);
			round.SetPhase(Phase.ConnectionConfirmation);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
			Assert.Equal(0, arena.Prison.CountInmates().noted);
			Assert.Equal(0, arena.Prison.CountInmates().banned);

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
			var a1 = WabiSabiFactory.CreateAlice();
			var a2 = WabiSabiFactory.CreateAlice();
			var a3 = WabiSabiFactory.CreateAlice();
			var a4 = WabiSabiFactory.CreateAlice();
			a1.ConfirmedConnetion = true;
			a2.ConfirmedConnetion = true;
			a3.ConfirmedConnetion = false;
			a4.ConfirmedConnetion = false;
			round.Alices.Add(a1);
			round.Alices.Add(a2);
			round.Alices.Add(a3);
			round.Alices.Add(a4);
			round.SetPhase(Phase.ConnectionConfirmation);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);
			Assert.Equal(2, round.Alices.Count);
			Assert.Equal(2, arena.Prison.CountInmates().noted);
			Assert.Equal(0, arena.Prison.CountInmates().banned);

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
			var a1 = WabiSabiFactory.CreateAlice();
			var a2 = WabiSabiFactory.CreateAlice();
			var a3 = WabiSabiFactory.CreateAlice();
			var a4 = WabiSabiFactory.CreateAlice();
			a1.ConfirmedConnetion = true;
			a2.ConfirmedConnetion = false;
			a3.ConfirmedConnetion = false;
			a4.ConfirmedConnetion = false;
			round.Alices.Add(a1);
			round.Alices.Add(a2);
			round.Alices.Add(a3);
			round.Alices.Add(a4);
			round.SetPhase(Phase.ConnectionConfirmation);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.DoesNotContain(round.Id, arena.Rounds.Keys);
			Assert.Equal(3, arena.Prison.CountInmates().noted);
			Assert.Equal(0, arena.Prison.CountInmates().banned);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
