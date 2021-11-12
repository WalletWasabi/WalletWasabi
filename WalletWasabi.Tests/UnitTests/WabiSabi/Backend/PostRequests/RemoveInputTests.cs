using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests
{
	public class RemoveInputTests
	{
		[Fact]
		public async Task SuccessAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var initialRemaining = round.RemainingInputVsizeAllocation;
			var alice = WabiSabiFactory.CreateAlice(round);
			round.Alices.Add(alice);
			Assert.True(round.RemainingInputVsizeAllocation < initialRemaining);

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			// There's no such alice yet, so success.
			var req = new InputsRemovalRequest(round.Id, Guid.NewGuid());
			await arena.RemoveInputAsync(req, CancellationToken.None);

			// There was the alice we want to remove so success.
			req = new InputsRemovalRequest(round.Id, alice.Id);
			await arena.RemoveInputAsync(req, CancellationToken.None);

			// Ensure that removing an alice freed up the input vsize
			// allocation from the round
			Assert.Equal(initialRemaining, round.RemainingInputVsizeAllocation);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();

			var req = new InputsRemovalRequest(uint256.Zero, Guid.NewGuid());
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RemoveInputAsync(req, CancellationToken.None));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = arena.Rounds.First();

			var req = new InputsRemovalRequest(round.Id, Guid.NewGuid());
			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.SetPhase(phase);
					var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RemoveInputAsync(req, CancellationToken.None));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
