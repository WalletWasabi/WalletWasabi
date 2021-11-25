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

		public Result Process(ICommand command, IState aggregateState)
		{
			if (aggregateState is not RoundState2 roundState)
			{
				throw new ArgumentException($"State should be type of {nameof(RoundState2)}.", nameof(aggregateState));
			}

			return command switch
			{
				StartRoundCommand cmd => Process(cmd, roundState),
				InputRegisterCommand cmd => Process(cmd, roundState),
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
