using NBitcoin;
using System.Collections.Immutable;
using System.Threading.Tasks;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Coordinator.WabiSabi;

/// <summary>
/// Tests for <see cref="Arena"/>
/// </summary>
public class ArenaTests
{
	/// <summary>
	/// Checks that <see cref="Arena.OccupiesParallelizationSlot"/> respects <see cref="WabiSabiConfig.RoundParallelizationOffset"/>.
	/// </summary>
	[Theory]
	// Positive offsets.
	[InlineData("InputRegistrationRunning", 1, 60, true)]                 // Offset 1 minute, 1 hour left of the input-registration phase remains -> cannot start a new round
	[InlineData("InputRegistrationRunning", 59, 60, true)]                // Offset 59 minutes, 1 hour left -> can start a new round
	[InlineData("InputRegistrationRunning", 61, 60, false)]               // Less than 61 minutes left of the input-registration phase remains -> can start a new round
	[InlineData("InputRegistrationRunning", 120, 60, false)]              // Offset longer than the whole phase -> can start a new round
	[InlineData("InputRegistrationEnded", 60, 60, false)]                 // Input registration over -> can start a new round
	// Zero offset.
	[InlineData("InputRegistrationRunning", 0, 60, true)]                 // Still open.
	[InlineData("InputRegistrationRunning", 0, 0, false)]                 // Time frame expired.
	[InlineData("InputRegistrationEnded", 0, 60, false)]                  // Ended early.
	// Negative offsets.
	[InlineData("InputRegistrationRunning", -5, 60, true)]                // Slot held for the whole input-registration phase -> cannot start a new round
	[InlineData("InputRegistrationEnded", -5, 60, true)]                  // Just ended, gap not elapsed -> cannot start a new round
	[InlineData("InputRegistrationEnded-wait150ms", -0.0005, 60, false)]  // Gap elapsed -> can start a new round
	[InlineData("AbortedNotEnoughAlices", -5, 60, false)]                 // Gets a new chance sooner -> can start a new round
	[InlineData("AbortedWithError", -5, 60, true)]                        // Other end states hold the slot for the gap -> cannot start a new round
	// Blame rounds never occupy a slot.  
	[InlineData("Blame", 1, 60, false)]
	[InlineData("Blame", 0, 60, false)]
	[InlineData("Blame", -5, 60, false)]
	public async Task OccupiesParallelizationSlotAsync(string scenario, double parallelizationOffsetMinutes, int inputRegistrationTimeoutMinutes, bool expectedOccupied)
	{
		var round = CreateRound(scenario, TimeSpan.FromMinutes(inputRegistrationTimeoutMinutes));

		// InputRegistrationEnd cannot be backdated, so we need to wait a bit here.
		if (scenario == "InputRegistrationEnded-wait150ms")
		{
			await Task.Delay(150, TestContext.Current.CancellationToken);
		}

		bool actual = Arena.OccupiesParallelizationSlot(round, TimeSpan.FromMinutes(parallelizationOffsetMinutes));
		Assert.Equal(expectedOccupied, actual);
	}

	private static Round CreateRound(string scenario, TimeSpan inputRegistrationTimeout)
	{
		var cfg = WabiSabiFactory.CreateWabiSabiConfig() with
		{
			StandardInputRegistrationTimeout = inputRegistrationTimeout
		};
		var parameters = WabiSabiFactory.CreateRoundParameters(cfg);
		var round = new Round(parameters, SecureRandom.Instance);

		switch (scenario)
		{
			// The round is in the input registration phase.
			case "InputRegistrationRunning":
				break;

			// Input registration ended (e.g. max input count reached) and the round moved on.
			case "InputRegistrationEnded":
			case "InputRegistrationEnded-wait150ms":
				round.SetPhase(Phase.ConnectionConfirmation);
				break;

			// The round ended with "AbortedNotEnoughAlices".
			case "AbortedNotEnoughAlices":
				round.EndRound(EndRoundState.AbortedNotEnoughAlices);
				break;

			// The round ended with "AbortedWithError".
			case "AbortedWithError":
				round.EndRound(EndRoundState.AbortedWithError);
				break;

			// The round is a blame round.
			case "Blame":
				return new BlameRound(parameters, round, ImmutableHashSet<OutPoint>.Empty, SecureRandom.Instance);
			default:
				throw new NotSupportedException($"Scenario '{scenario}' is not supported");
		}

		return round;
	}
}
