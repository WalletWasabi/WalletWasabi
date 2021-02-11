using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class InputRegistrationTests
	{
		[Fact]
		public async Task RoundNotFoundAsync()
		{
			var arena = new MockArena();
			arena.OnTryGetRound = (roundId) => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena);
			var req = new InputsRegistrationRequest(
				Guid.NewGuid(),
				null!,
				null!,
				null!);
			Assert.Throws<InvalidOperationException>(() => handler.RegisterInput(req));
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			var arena = new MockArena();
			var round = new Round();
			arena.OnTryGetRound = (roundId) => round;

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena);
					var req = new InputsRegistrationRequest(
						round.Id,
						null!,
						null!,
						null!);
					Assert.Throws<InvalidOperationException>(() => handler.RegisterInput(req));
				}
			}
		}
	}
}
