using WalletWasabi.EventSourcing;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;

public record TestWrappedEvent(long SequenceId, string Value) : WrappedEvent(SequenceId);
