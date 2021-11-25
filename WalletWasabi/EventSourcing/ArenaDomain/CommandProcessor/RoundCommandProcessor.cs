using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
		public Result Process(StartRoundCommand command, RoundState2 state)
		{
			var errors = PrepareErrors();
			if (!IsStateValid(Phase.New, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return errors.Count > 0 ?
				Result.Fail(errors) :
				Result.Succeed(
					new[] { new RoundStartedEvent(command.RoundParameters) });
		}

		public Result Process(InputRegisterCommand command, RoundState2 state)
		{
			var errors = PrepareErrors();
			if (!IsStateValid(Phase.InputRegistration, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return errors.Count > 0 ?
				Result.Fail(errors) :
				Result.Succeed(
					new[] { new InputRegisteredEvent(command.AliceId, command.Coin, command.OwnershipProof) });
		}

		public Result Process(EndRoundCommand command, RoundState2 state)
		{
			return Result.Succeed(new RoundEndedEvent());
		}

		public Result Process(InputConnectionConfirmedCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputConnectionConfirmedEvent(command.AliceId, command.Coin, command.OwnershipProof));
		}

		public Result Process(RemoveInputCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputUnregistered(command.AliceId));
		}

		public Result Process(RegisterOutputCommand command, RoundState2 state)
		{
			return Result.Succeed(new OutputRegisteredEvent(command.Script, command.Value));
		}

		public Result Process(StartOutputRegistrationCommand command, RoundState2 state)
		{
			return Result.Succeed(new OutputRegistrationStartedEvent());
		}

		public Result Process(StartConnectionConfirmationCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputsConnectionConfirmationStartedEvent());
		}

		public Result Process(StartTransactionSigningCommand command, RoundState2 state)
		{
			return Result.Succeed(new SigningStartedEvent());
		}

		public Result Process(SucceedRoundCommand command, RoundState2 state)
		{
			return Result.Succeed(new IEvent[] { new RoundSucceedEvent(), new RoundEndedEvent() });
		}

		public Result Process(InputReadyToSignCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputReadyToSignEvent(command.AliceId));
		}

		public Result Process(AddSignatureEvent command, RoundState2 state)
		{
			return Result.Succeed(new SignatureAddedEvent(command.AliceId, command.WitScript));
		}

		public Result Process(ICommand command, IState state)
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

		private static ImmutableArray<IError>.Builder PrepareErrors()
		{
			return ImmutableArray.CreateBuilder<IError>();
		}

		private bool IsStateValid(Phase expected, RoundState2 state, string commandName, out Result errorResult)
		{
			var isStateValid = expected == state.Phase;
			errorResult = null!;
			if (!isStateValid)
			{
				errorResult = Result.Fail(
					new Error(
						$"Unexpected State for '{commandName}'. expected: '{expected}', actual: '{state.Phase}'"));
			}
			return isStateValid;
		}
	}
}
