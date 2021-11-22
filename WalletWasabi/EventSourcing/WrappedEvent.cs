namespace WalletWasabi.EventSourcing
{
	public record WrappedEvent(long SequenceId, object DomainEvent, Guid SourceId);
}
