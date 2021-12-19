namespace WalletWasabi.EventSourcing;

/// <param name="SequenceId">Only positive integers are allowed.</param>
public record WrappedEvent(long SequenceId);
