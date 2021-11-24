using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public record TestWrappedEvent(long SequenceId, string Value = "", IEvent? DomainEvent = null, Guid SourceId = default) : WrappedEvent(SequenceId, DomainEvent!, SourceId);
}
