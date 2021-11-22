using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IEventStore
	{
		Task ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId);

		Task<IAggregate> GetAggregateAsync(string aggregateType, string aggregateId);
	}
}
