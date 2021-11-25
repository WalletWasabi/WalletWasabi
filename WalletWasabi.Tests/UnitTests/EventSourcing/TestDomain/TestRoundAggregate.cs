using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	internal class TestRoundAggregate : IAggregate
	{
		public TestRoundState State { get; private set; } =
			new(0, TestRoundStatusEnum.New, Array.Empty<TestRoundInputState>(), null, null);

		IState IAggregate.State => State;

		public void Apply(RoundStarted @event)
		{
			State = State with
			{
				MinInputSats = @event.MinInputSats
			};
		}

		public void Apply(InputRegistered @event)
		{
			State = State with
			{
				Inputs = new List<TestRoundInputState>
				{
					new(@event.InputId, @event.Sats)
				}.AsReadOnly()
			};
		}

		public void Apply(InputUnregistered @event)
		{
			State = State with
			{
				Inputs = State.Inputs.Where(a => a.InputId != @event.InputId).ToList().AsReadOnly(),
			};
		}

		public void Apply(SigningStarted @event)
		{
			State = State with
			{
				Status = TestRoundStatusEnum.Signing,
			};
		}

		public void Apply(RoundSucceeded @event)
		{
			State = State with
			{
				Status = TestRoundStatusEnum.Succeeded,
				TxId = @event.TxId,
			};
		}

		public void Apply(RoundFailed @event)
		{
			State = State with
			{
				Status = TestRoundStatusEnum.Failed,
				FailureReason = @event.Reason,
			};
		}

		public void Apply(IEvent ev)
		{
			throw new NotImplementedException();
		}
	}
}
