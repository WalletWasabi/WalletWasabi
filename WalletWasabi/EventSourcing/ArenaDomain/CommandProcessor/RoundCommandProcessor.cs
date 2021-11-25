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

		public IEnumerable<IEvent> Process(InputConnectionConfirmedCommand command, RoundState2 aggregate)
		{
			return new[] { new InputConnectionConfirmedEvent(command.AliceId, command.Coin, command.OwnershipProof) };
		}

		public IEnumerable<IEvent> Process(RemoveInputCommand command, RoundState2 aggregate)
		{
			return new[] { new InputUnregistered(command.AliceId) };
		}

		public IEnumerable<IEvent> Process(RegisterOutputCommand command, RoundState2 aggregate)
		{
			return new[] { new OutputRegisteredEvent(command.Script, command.CredentialAmount) };
		}

		public IEnumerable<IEvent> Process(StartOutputRegistrationCommand command, RoundState2 aggregate)
		{
			return new[] { new OutputRegistrationStartedEvent() };
		}

		public IEnumerable<IEvent> Process(StartConnectionConfirmationCommand command, RoundState2 aggregate)
		{
			return new[] { new InputsConnectionConfirmationStartedEvent() };
		}

		public IEnumerable<IEvent> Process(StartTransactionSigningCommand command, RoundState2 aggregate)
		{
			return new[] { new SigningStartedEvent() };
		}

		public IEnumerable<IEvent> Process(SucceedRoundCommand command, RoundState2 aggregate)
		{
			return new[] { new RoundSucceedEvent() };
		}

		public IEnumerable<IEvent> Process(InputReadyToSignCommand command, RoundState2 aggregate)
		{
			return new[] { new InputReadyToSignEvent(command.AliceId) };
		}

		public IEnumerable<IEvent> Process(AddSignatureEvent command, RoundState2 aggregate)
		{
			return new[] { new SignatureAddedEvent(command.AliceId, command.WitScript) };
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
				InputConnectionConfirmedCommand cmd => Process(cmd, roundState),
				RemoveInputCommand cmd => Process(cmd, roundState),
				RegisterOutputCommand cmd => Process(cmd, roundState),
				StartOutputRegistrationCommand cmd => Process(cmd, roundState),
				StartConnectionConfirmationCommand cmd => Process(cmd, roundState),
				StartTransactionSigningCommand cmd => Process(cmd, roundState),
				SucceedRoundCommand cmd => Process(cmd, roundState),
				InputReadyToSignCommand cmd => Process(cmd, roundState),
				AddSignatureEvent cmd => Process(cmd, roundState),

				_ => throw new InvalidOperationException()
			};
		}
	}
}
