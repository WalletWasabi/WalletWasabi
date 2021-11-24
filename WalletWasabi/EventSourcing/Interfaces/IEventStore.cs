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
		///
		/// </summary>
		/// <param name="command"></param>
		/// <param name="aggregateType"></param>
		/// <param name="aggregateId"></param>
		/// <returns></returns>
		/// <exception cref="CommandFailedException">If command processor returns an error.</exception>
		/// <exception cref="OptimisticConcurrencyException">If conflict happend even after retrying.</exception>
		Task<WrappedResult> ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId);

		Task<IAggregate> GetAggregateAsync(string aggregateType, string aggregateId);
	}
}
