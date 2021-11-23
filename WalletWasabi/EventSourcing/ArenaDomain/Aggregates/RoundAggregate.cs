using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain.Aggregates;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.EventSourcing.ArenaDomain
{
	public class RoundAggregate : IAggregate
	{
		public RoundState2 State { get; private set; }

		private void Apply(RoundStartedEvent ev)
		{
			State = State with { RoundParameters = ev.RoundParameters, Phase = Phase.InputRegistration };
		}

		private void Apply(InputRegisteredEvent ev)
		{
			State = State with { Inputs = State.Inputs.Add(new InputState(ev.Coin, ev.OwnershipProof, ev.AliceId)) };
		}

		private void Apply(InputUnregistered ev)
		{
			State = State with { Inputs = State.Inputs.RemoveAll(input => input.AliceId == ev.AliceId) };
		}

		private void Apply(InputsConnectionConfirmationStartedEvent _)
		{
			State = State with { Phase = Phase.ConnectionConfirmation };
		}

		private void Apply(InputConnectionConfirmedEvent ev)
		{
			var index = State.Inputs.FindIndex(input => input.AliceId == ev.AliceId);
			if (index < 0)
			{
				return;
			}

			State = State with { Inputs = State.Inputs.SetItem(index, State.Inputs[index] with { ConnectionConfirmed = true }) };
		}

		private void Apply(OutputRegistrationStartedEvent _)
		{
			State = State with { Phase = Phase.OutputRegistration };
		}

		public void Apply(IEvent ev)
		{
			switch (ev)
			{
				case RoundStartedEvent eve:
					Apply(eve);
					break;

				case InputRegisteredEvent eve:
					Apply(eve);
					break;

				case InputUnregistered eve:
					Apply(eve);
					break;

				case InputsConnectionConfirmationStartedEvent eve:
					Apply(eve);
					break;

				case InputConnectionConfirmedEvent eve:
					Apply(eve);
					break;

				case OutputRegistrationStartedEvent eve:
					Apply(eve);
					break;

				default:
					throw new InvalidOperationException();
			}
		}
	}
}
