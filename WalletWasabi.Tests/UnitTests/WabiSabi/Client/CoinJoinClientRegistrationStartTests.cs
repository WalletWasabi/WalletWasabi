using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <seealso href="https://github.com/WalletWasabi/WalletWasabi/issues/14924"/>
public class CoinJoinClientRegistrationStartTests
{
	private static readonly TimeSpan Buffer = TimeSpan.FromMinutes(1);
	private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

	private static RoundState CreateRoundState(DateTimeOffset inputRegistrationStart, bool isBlame = false)
	{
		WabiSabiConfig cfg = new();
		Round round = WabiSabiFactory.CreateRound(cfg);
		if (isBlame)
		{
			round = WabiSabiFactory.CreateBlameRound(round, cfg);
		}

		round.InputRegistrationTimeFrame = new TimeFrame(
			inputRegistrationStart,
			round.InputRegistrationTimeFrame.Duration);

		return RoundState.FromRound(round);
	}

	[Fact]
	public void DoesNotRegisterWhileThePreviousRoundIsStillInInputRegistration()
	{
		// The coordinator creates the successor round when the previous one has one minute of input
		// registration left, so a round this young overlaps with that still-open round.
		var roundState = CreateRoundState(Now - TimeSpan.FromSeconds(5));

		var registrationStart = CoinJoinClient.GetRegistrationStart(roundState, Buffer, Now);

		Assert.Equal(Now + TimeSpan.FromSeconds(55), registrationStart);
	}

	[Fact]
	public void RegistersImmediatelyOnceTheBufferPeriodHasPassed()
	{
		var roundState = CreateRoundState(Now - Buffer - TimeSpan.FromSeconds(1));

		var registrationStart = CoinJoinClient.GetRegistrationStart(roundState, Buffer, Now);

		Assert.Equal(Now, registrationStart);
	}

	[Fact]
	public void BlameRoundsAreNotHeldBack()
	{
		// A blame round's participants are exactly the failed round's participants, so there is
		// nothing to hide and its input registration only lasts a few minutes.
		var roundState = CreateRoundState(Now, isBlame: true);

		var registrationStart = CoinJoinClient.GetRegistrationStart(roundState, Buffer, Now);

		Assert.Equal(Now, registrationStart);
	}

	[Fact]
	public void NoTimeLimitMeansNoHoldBack()
	{
		// Tests construct the client with TimeSpan.Zero - registration must not be delayed then.
		var roundState = CreateRoundState(Now);

		var registrationStart = CoinJoinClient.GetRegistrationStart(roundState, TimeSpan.Zero, Now);

		Assert.Equal(Now, registrationStart);
	}
}
