namespace WalletWasabi.WabiSabi.Models.EventSourcing
{
	public class RoundsAggregate : Aggregate
	{
		public RoundsState State { get; private set; } = new ();

		public override void Apply(RoundCreated roundCreatedEvent)
		{
			State = State with { Rounds = State.Rounds.Add(roundCreatedEvent.Round) };
		}
	}
}
