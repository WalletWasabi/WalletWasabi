using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.ArenaDomain
{
	public class RoundAggregate : IAggregate
	{
		private void Apply(RoundStartedEvent roundStartedEvent)
		{
		}

		public void Apply(IEvent ev)
		{
			switch (ev)
			{
				case RoundStartedEvent roundStartedEvent:
					Apply(roundStartedEvent);
					break;

				default:
					throw new InvalidOperationException();
			}
		}
	}
}
