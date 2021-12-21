using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.Records
{
	public record WrappedEvent(long SequenceId, IEvent DomainEvent, Guid SourceId);
}
