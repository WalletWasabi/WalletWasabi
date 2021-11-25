using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.Exceptions;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IEventStore
	{
		/// <summary>
		/// Process <paramref name="command"/> on aggregate in strongly sequentially consistent
		/// way. Using optimistic strategy with automatic retry in case of conflict. Skips
		/// command in case of command with same <seealso cref="ICommand.IdempotenceId"/> has
		/// been already successfully processed.
		/// </summary>
		/// <param name="command">command to be processed</param>
		/// <param name="aggregateType">type of aggregate</param>
		/// <param name="aggregateId">id of aggregate</param>
		/// <returns>returns state right after command has been processed</returns>
		/// <exception cref="CommandFailedException">If command processor returns an error.</exception>
		/// <exception cref="OptimisticConcurrencyException">If conflict happend even after retrying.</exception>
		Task<WrappedResult> ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId);

		/// <summary>
		/// Replays all events and returns the current state of an aggregate
		/// </summary>
		/// <param name="aggregateType">type of aggregate</param>
		/// <param name="aggregateId">id of aggregate</param>
		/// <returns>Returns aggregate in current state</returns>
		Task<IAggregate> GetAggregateAsync(string aggregateType, string aggregateId);
	}
}
