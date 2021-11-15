namespace WalletWasabi.WabiSabi.Models.EventSourcing
{
	public abstract class Aggregate
	{
		public virtual void Apply(RoundCreated roundCreatedEvent)
		{
		}
		public virtual void Apply(InputAdded inputAddedEvent)
		{
		}
		public virtual void Apply(OutputAdded outputAddedEvent)
		{
		}
		public virtual void Apply(WitnessAdded witnessAddedEvent)
		{
		}
		public virtual void Apply(StatePhaseChanged stateChangedEvent)
		{
		}
	}
}
