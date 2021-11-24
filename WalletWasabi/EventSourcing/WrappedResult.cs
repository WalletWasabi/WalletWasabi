using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Result of successfully processed and persisted command.
	/// </summary>
	public record WrappedResult(
		long LastSequenceId,
		IReadOnlyList<WrappedEvent> NewEvents,
		IState State,
		bool IdempotenceIdDuplicate = false);
}
