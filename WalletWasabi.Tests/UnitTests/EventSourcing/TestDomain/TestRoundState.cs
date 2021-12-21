using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public record TestRoundState(
		ulong MinInputSats,
		TestRoundStatusEnum Status,
		ImmutableList<TestRoundInputState> Inputs,
		string? TxId,
		string? FailureReason) : IState;

	public record TestRoundInputState(string InputId, ulong Sats);
}
