using System.Collections.Generic;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public record TestRoundState(
		ulong MinInputSats,
		TestRoundStatusEnum Status,
		IReadOnlyList<TestRoundInputState> Inputs,
		string? TxId,
		string? FailureReason) : IState;

	public record TestRoundInputState(string InputId, ulong Sats);
}
