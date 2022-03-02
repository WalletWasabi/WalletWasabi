using FluentAssertions;
using WalletWasabi.Fluent.State;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class StateMachineTests
{
	[Fact]
	public void Initialization_has_initial_state()
	{
		StateMachine<PhoneState, PhoneState> sut = new(PhoneState.Disconnected);
		sut.State.Should().Be(PhoneState.Disconnected);
	}

	[Fact]
	public void Permitted_trigger_transitions_to_next_state()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected);

		sut.Fire(PhoneTrigger.Connect);

		sut.State.Should().Be(PhoneState.Connected);
	}

	[Fact]
	public void Initialization_should_not_execute_entry_actions()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var hasEntered = false;
		sut.Configure(PhoneState.Disconnected)
			.OnEntry(() => hasEntered = true);

		hasEntered.Should().BeFalse();
	}

	[Fact]
	public void Starting_after_initialization_executes_entry_actions()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var hasEntered = false;
		sut.Configure(PhoneState.Disconnected)
			.OnEntry(() => hasEntered = true);

		sut.Start();

		hasEntered.Should().BeTrue();
	}

	[Fact]
	public void Exiting_state_with_exit_action_should_execute_it()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var hasExited = false;
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected)
			.OnExit(() => hasExited = true);

		sut.Fire(PhoneTrigger.Connect);

		hasExited.Should().BeTrue();
	}

	[Fact]
	public void Entering_state_with_entry_action_should_execute_it()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var hasEntered = false;
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected);
		sut.Configure(PhoneState.Connected)
			.OnEntry(() => hasEntered = true);

		sut.Fire(PhoneTrigger.Connect);

		hasEntered.Should().BeTrue();
	}

	[Fact]
	public void Entering_substate_does_not_execute_exit_actions_on_parent()
	{
		var hasExited = false;

		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected);
		sut.Configure(PhoneState.OnHold)
			.SubstateOf(PhoneState.Connected)
			.OnExit(() => hasExited = true);
		sut.Configure(PhoneState.Connected)
			.Permit(PhoneTrigger.PutOnHold, PhoneState.OnHold);

		sut.Fire(PhoneTrigger.Connect);
		sut.Fire(PhoneTrigger.PutOnHold);

		hasExited.Should().BeFalse();
	}

	[Fact]
	public void Exiting_substate_does_not_call_entry_actions_on_parent()
	{
		var connectedCount = 0;

		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected);

		sut.Configure(PhoneState.OnHold)
			.SubstateOf(PhoneState.Connected)
			.OnEntry(() => connectedCount++);
		sut.Configure(PhoneState.Connected)
			.Permit(PhoneTrigger.PutOnHold, PhoneState.OnHold);

		sut.Configure(PhoneState.OnHold)
			.Permit(PhoneTrigger.ReleaseOnHold, PhoneState.Connected);

		sut.Fire(PhoneTrigger.Connect);
		sut.Fire(PhoneTrigger.PutOnHold);
		sut.Fire(PhoneTrigger.ReleaseOnHold);

		connectedCount.Should().Be(1);
	}

	private enum PhoneState
	{
		Disconnected,
		OnHold,
		Connected
	}

	private enum PhoneTrigger
	{
		Connect,
		PutOnHold,
		ReleaseOnHold
	}
}