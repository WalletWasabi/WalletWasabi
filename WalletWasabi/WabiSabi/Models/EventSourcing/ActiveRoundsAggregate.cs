namespace WalletWasabi.WabiSabi.Models.EventSourcing
{
	public class ActiveRoundsAggregate : Aggregate
	{
		public ActiveRoundsState State { get; private set; } = new ();

		public override void Apply(RoundCreated roundCreatedEvent)
		{
			State = State with { Rounds = State.Rounds.Add(roundCreatedEvent.Round) };
		}
	}
}
