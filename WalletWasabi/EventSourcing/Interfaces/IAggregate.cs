using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IAggregate
	{
		IState State { get; }

		void Apply(IEvent ev);
	}
}
