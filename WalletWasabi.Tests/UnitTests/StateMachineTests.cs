using FluentAssertions;
using Stateless;
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

		sut.State.Should().Be(State.Locked);
	}

	[Fact]
	public void After_start_has_default_state()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		sut.State.Should().Be(State.Locked);
	}

	[Fact]
	public void Permitted_trigger_sets_next_state()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		sut.Configure(State.Locked)
			.Permit(Trigger.Coin, State.Unlocked);

		sut.Fire(Trigger.Coin);

		sut.State.Should().Be(State.Unlocked);
	}

	[Fact]
	public void Just_initialized_should_not_execute_entry_action()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		var entered = false;
		sut.Configure(State.Locked)
			.OnEntry(() => entered = true);

		entered.Should().BeFalse();
	}

	[Fact]
	public void State_with_exit_action_should_execute_action_when_transitioned()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		var entered = false;
		sut.Configure(State.Locked)
			.Permit(Trigger.Coin, State.Unlocked)
			.OnExit(() => entered = true);

		sut.Fire(Trigger.Coin);

		entered.Should().BeTrue();
	}

	[Fact]
	public void Transitioning_to_state_with_entry_action_should_execute_the_entry_action()
	{
		StateMachine<State, Trigger> sut = new(State.Locked);
		var entered = false;
		sut.Configure(State.Locked)
			.Permit(Trigger.Coin, State.Unlocked);
		sut.Configure(State.Unlocked)
			.OnEntry(() => entered = true);

		sut.Fire(Trigger.Coin);

		entered.Should().BeTrue();
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