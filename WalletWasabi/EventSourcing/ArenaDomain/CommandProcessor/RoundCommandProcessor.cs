using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain.Aggregates;
using WalletWasabi.EventSourcing.ArenaDomain.Command;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.EventSourcing.ArenaDomain.CommandProcessor
{
	public class RoundCommandProcessor : ICommandProcessor
	{
		public IEnumerable<IEvent> Process(StartRoundCommand command, RoundState2 aggregate)
		{
			return new[] { new RoundStartedEvent(command.RoundParameters) };
		}

		public IEnumerable<IEvent> Process(InputRegisterCommand command, RoundState2 aggregate)
		{
			return new[] { new InputRegisteredEvent(command.AliceId, command.Coin, command.OwnershipProof) };
		}

		public IEnumerable<IEvent> Process(EndRoundCommand command, RoundState2 aggregate)
		{
			return new[] { new RoundEndedEvent() };
		}

		public IEnumerable<IEvent> Process(ICommand command, IState state)
		{
			if (state is not RoundState2 roundState)
			{
				throw new ArgumentException($"State should be type of {nameof(RoundState2)}.", nameof(state));
			}

			return command switch
			{
				StartRoundCommand cmd => Process(cmd, roundState),
				InputRegisterCommand cmd => Process(cmd, roundState),
				EndRoundCommand cmd => Process(cmd, roundState),
				_ => throw new InvalidOperationException()
			};
		}
	}
}
