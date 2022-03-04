using WalletWasabi.Fluent.State;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class StateMachineTests
{
	[Fact]
	public void Initialization_has_initial_state()
	{
		StateMachine<PhoneState, PhoneState> sut = new(PhoneState.Disconnected);
		Assert.Equal(PhoneState.Disconnected, sut.State);
	}

	[Fact]
	public void Permitted_trigger_transitions_to_next_state()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected);

		sut.Fire(PhoneTrigger.Connect);

		Assert.Equal(PhoneState.Connected, sut.State);
	}

	[Fact]
	public void Initialization_should_not_execute_entry_actions()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var hasEntered = false;
		sut.Configure(PhoneState.Disconnected)
			.OnEntry(() => hasEntered = true);

		Assert.False(hasEntered);
	}

	[Fact]
	public void Starting_after_initialization_executes_entry_actions()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var hasEntered = false;
		sut.Configure(PhoneState.Disconnected)
			.OnEntry(() => hasEntered = true);

		sut.Start();

		Assert.True(hasEntered);
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

		Assert.True(hasExited);
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

		Assert.True(hasEntered);
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

		Assert.False(hasExited);
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

		Assert.Equal(1, connectedCount);
	}

	[Fact]
	public void Firing_special_trigger_executes_given_action()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var triggered = false;
		sut.Configure(PhoneState.Disconnected)
			.OnTrigger(PhoneTrigger.Ping, () => triggered = true);

		sut.Fire(PhoneTrigger.Ping);

		Assert.True(triggered);
	}

	[Fact]
	public void Firing_special_trigger_executes_given_multiple_actions()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		int count = 0;
		sut.Configure(PhoneState.Disconnected)
			.OnTrigger(PhoneTrigger.Ping, () => count++)
			.OnTrigger(PhoneTrigger.Ping, () => count++)
			.OnTrigger(PhoneTrigger.Ping, () => count++);

		sut.Fire(PhoneTrigger.Ping);

		Assert.Equal(3, count);
	}

	
	[Fact]
	public void Firing_special_trigger_executes_correct_action()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);
		var executedAction = "none";
		sut.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Connect, PhoneState.Connected)
			.OnTrigger(PhoneTrigger.Ping, () => executedAction = "first");
		sut.Configure(PhoneState.Connected)
			.OnTrigger(PhoneTrigger.Ping, () => executedAction = "second");

		sut.Fire(PhoneTrigger.Connect);
		sut.Fire(PhoneTrigger.Ping);

		Assert.Equal("second", executedAction);
	}

	[Fact]
	public void Permitting_trigger_to_same_transition_should_not_be_allowed()
	{
		StateMachine<PhoneState, PhoneTrigger> sut = new(PhoneState.Disconnected);

		void ConfigureForReEntry() => sut
			.Configure(PhoneState.Disconnected)
			.Permit(PhoneTrigger.Ping, PhoneState.Disconnected);

		Assert.Throws<InvalidOperationException>(ConfigureForReEntry);
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
		ReleaseOnHold,
		Ping
	}
}