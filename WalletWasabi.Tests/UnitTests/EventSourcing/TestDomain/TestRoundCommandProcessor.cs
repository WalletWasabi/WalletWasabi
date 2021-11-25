using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestRoundCommandProcessor : ICommandProcessor
	{
		public const ulong MaxSats = 21_000_000_00_000_000UL;

		public Result Process(StartRound command, TestRoundState state)
		{
			var errors = ImmutableArray.CreateBuilder<IError>();
			if (!IsStateValid(TestRoundStatusEnum.New, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return 0 < errors.Count ?
				Result.Fail(errors) :
				Result.Succeed(new RoundStarted(command.MinInputSats));
		}

		public Result Process(RegisterInput command, TestRoundState state)
		{
			var errors = ImmutableArray.CreateBuilder<IError>();
			if (!IsStateValid(TestRoundStatusEnum.Started, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			if (state.Inputs.Any(a => a.InputId == command.InputId))
			{
				errors.Add(new Error(nameof(command.InputId), $"input with inputId: '{command.InputId}' is already registered"));
			}
			if (MaxSats < command.Sats)
			{
				errors.Add(new Error(nameof(command.Sats), $"Too much Sats. MaxSats: '{MaxSats}'"));
			}
			return 0 < errors.Count ?
				Result.Fail(errors) :
				Result.Succeed(new InputRegistered(command.InputId, command.Sats));
		}

		public Result Process(UnregisterInput command, TestRoundState state)
		{
			var errors = new List<IError>();
			if (!IsStateValid(TestRoundStatusEnum.Started, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			if (!state.Inputs.Any(a => a.InputId == command.InputId))
			{
				errors.Add(new Error(nameof(command.InputId), $"input with inputId: '{command.InputId}' is not registered"));
			}
			return 0 < errors.Count ?
				Result.Fail(errors) :
				Result.Succeed(new InputUnregistered(command.InputId));
		}

		public Result Process(StartSigning command, TestRoundState state)
		{
			var errors = new List<IError>();
			if (!IsStateValid(TestRoundStatusEnum.Started, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			if (state.Inputs.Count <= 0)
			{
				errors.Add(new Error($"There are no inputs registered. Cannot {nameof(StartSigning)}"));
			}
			return 0 < errors.Count ?
				Result.Fail(errors) :
				Result.Succeed(new SigningStarted());
		}

		public Result Process(SetSucceeded command, TestRoundState state)
		{
			if (!IsStateValid(TestRoundStatusEnum.Signing, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return Result.Succeed(new RoundSucceeded(command.TxId));
		}

		public Result Process(SetFailed command, TestRoundState state)
		{
			if (!IsStateValid(TestRoundStatusEnum.Signing, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return Result.Succeed(new RoundFailed(command.Reason));
		}

		Result ICommandProcessor.Process(ICommand command, IState state)
		{
			return ProcessDynamic(command, (TestRoundState)state);
		}

		private Result ProcessDynamic(dynamic command, TestRoundState state)
		{
			return this.Process(command, state);
		}

		private bool IsStateValid(TestRoundStatusEnum expected, TestRoundState state, string commandName, out Result errorResult)
		{
			var isStateValid = expected != state.Status;
			errorResult = null!;
			if (!isStateValid)
			{
				errorResult = Result.Fail(
					new Error(
						$"unexpected State for '{commandName}'. expected: '{expected}', actual: '{state.Status}'"));
			}
			return isStateValid;
		}
	}
}
