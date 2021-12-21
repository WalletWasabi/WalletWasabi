using System.Collections.Generic;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.Records
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
