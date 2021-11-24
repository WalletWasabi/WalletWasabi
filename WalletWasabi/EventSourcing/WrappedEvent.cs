namespace WalletWasabi.EventSourcing
{
	public record WrappedEvent(long SequenceId, IEvent DomainEvent, Guid SourceId);
}
