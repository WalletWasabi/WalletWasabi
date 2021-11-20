using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Interfaces.EventSourcing
{
	public interface IEventRepository
	{
		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(
			string aggregateType,
			string? fromId = null,
			int? limit = null);

		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(
			string aggregateType,
			string id,
			long fromSequenceId = 0,
			int? limit = null);

		/// <exception cref="OptimisticConcurrencyException">if there is concurrency conflict ; retry command</exception>
		/// <exception cref="ArgumentException">invalid input</exception>
		public Task AppendEventsAsync(
			string aggregateType,
			string id,
			IEnumerable<WrappedEvent> wrappedEvents);
	}
}
