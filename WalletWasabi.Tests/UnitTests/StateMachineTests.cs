using FluentAssertions;
using WalletWasabi.Fluent.State;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests;

public class StateMachineTests
{
	private readonly ITestOutputHelper _output;

	public StateMachineTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Non_started_machine_has_default_state()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);

		sut.CurrentState.Should().Be(State.Locked);
	}

	[Fact]
	public void After_start_has_default_state()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		sut.CurrentState.Should().Be(State.Locked);
	}

	[Fact]
	public void Permitted_trigger_sets_next_state()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		sut.Configure(State.Locked)
			.Permit(Trigger.Coin, State.Unlocked);
		sut.Start();

		sut.Fire(Trigger.Coin);

		sut.CurrentState.Should().Be(State.Unlocked);
	}


	public enum Trigger
	{
		Coin,
		Pass
	}

	public enum State
	{
		Locked,
		Unlocked
	}
}