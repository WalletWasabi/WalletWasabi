using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Interfaces.EventSourcing
{
	public interface IEventRepository
	{
		/// <summary>
		/// Atomically persistently appends ordered list of events for given aggregate.
		/// In case of duplicate of <seealso cref="WrappedEvent.SequenceId"/>
		/// <seealso cref="OptimisticConcurrencyException"/> is thrown indicating that
		/// command should be retried with freshly loaded events from <see cref="ListEventsAsync"/>.
		/// </summary>
		/// <param name="aggregateType">Type of aggregate</param>
		/// <param name="aggregateId">Id of aggregate</param>
		/// <param name="wrappedEvents">Ordered list of events to be persisted</param>
		/// <exception cref="OptimisticConcurrencyException">If there is concurrency conflict ; retry command</exception>
		/// <exception cref="TransientException">Transient infrastructure failure</exception>
		/// <exception cref="ArgumentException">Invalid input</exception>
		public Task AppendEventsAsync(
			string aggregateType,
			string aggregateId,
			IEnumerable<WrappedEvent> wrappedEvents);

		/// <summary>
		/// List strongly ordered events of given aggregate. This is the primary source of truth.
		/// Events are used to reconstruct aggregate state before processing command.
		/// </summary>
		/// <param name="aggregateType">Type of aggregate</param>
		/// <param name="aggregateId">Id of aggregate</param>
		/// <param name="afterSequenceId">Starts with event after given <seealso cref="WrappedEvent.SequenceId"/>
		/// and lists all following events</param>
		/// <param name="maxCount">Limits the number of returned events</param>
		/// <returns>Ordered list of events</returns>
		/// <exception cref="TransientException">Transient infrastracture failure</exception>
		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(
			string aggregateType,
			string aggregateId,
			long afterSequenceId = 0,
			int? maxCount = null);

		/// <summary>
		/// Supplementary method for enumerating all ids for <paramref name="aggregateType"/>
		/// in this event repository. Order of ids is not defined can be any artificial.
		/// </summary>
		/// <param name="aggregateType">Type of aggregate</param>
		/// <param name="afterAggregateId">Starts with id right after given id and lists all following ids
		/// in any artificial order</param>
		/// <param name="maxCount">Limits the number of returned events</param>
		/// <returns>Unordered list of ids</returns>
		/// <exception cref="TransientException">Transient infrastracture failure</exception>
		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(
			string aggregateType,
			string? afterAggregateId = null,
			int? maxCount = null);
	}
}
