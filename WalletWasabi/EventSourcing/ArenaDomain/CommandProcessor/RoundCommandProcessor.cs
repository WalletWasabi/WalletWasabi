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
	public class RoundCommandProcessor : ICommandProcessor
	{
		public IEnumerable<IEvent> Process(StartRoundCommand command, RoundAggregate aggregate)
		{
			return new[] { new RoundStartedEvent(command.RoundParameters) };
		}

		public IEnumerable<IEvent> Process(ICommand command, IAggregate aggregate)
		{
			if (aggregate is not RoundAggregate)
			{
				throw new ArgumentException($"Aggregate should be type of {nameof(RoundAggregate)}.", nameof(aggregate));
			}

			return command switch
			{
				StartRoundCommand startRoundCommand => Process(startRoundCommand, aggregate),
				_ => throw new InvalidOperationException()
			};
		}
	}
}
