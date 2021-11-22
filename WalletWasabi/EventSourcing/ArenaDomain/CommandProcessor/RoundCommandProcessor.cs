using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain.Command;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.EventSourcing.ArenaDomain.CommandProcessor
{
	public class RoundCommandProcessor
	{
		public IEnumerable<IEvent> Process(StartRoundCommand command, RoundAggregate aggregate) //TODO
		{
			return new[] { new RoundStartedEvent(command.RoundParameters) };
		}
	}
}
