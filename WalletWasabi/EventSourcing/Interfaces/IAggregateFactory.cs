using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IAggregateFactory
	{
		IAggregate Create(string aggregateType);

		bool TryCreate(string aggregateType, out IAggregate aggregate);
	}
}
