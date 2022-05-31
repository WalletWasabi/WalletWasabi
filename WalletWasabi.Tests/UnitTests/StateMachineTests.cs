using WalletWasabi.Fluent.State;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class StateMachineTests
{
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

	private enum JukeBoxTrigger
	{
		Play,
		Pause
	}

	private enum JukeBoxState
	{
		Playing,
		Paused,
		PausedChild,
		PausedChild2
	}

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

	[Fact]
	public void Transition_direct_to_child_state_enters_via_parent()
	{
		StateMachine<JukeBoxState, JukeBoxTrigger> sut = new(JukeBoxState.Playing);

		sut.Configure(JukeBoxState.Playing)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild);

		bool pausedEntered = false;
		bool pausedChildEntered = false;
		int enteredPausedCount = 0;

		sut.Configure(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Play, JukeBoxState.Playing)
			.OnEntry(() =>
			{
				pausedEntered = true;
				enteredPausedCount++;
			});

		sut.Configure(JukeBoxState.PausedChild)
			.SubstateOf(JukeBoxState.Paused)
			.OnEntry(() =>
			{
				pausedChildEntered = true;
				enteredPausedCount++;
			});

		sut.Start();

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(JukeBoxState.PausedChild, sut.State);
		Assert.True(sut.IsInState(JukeBoxState.Paused));

		Assert.True(pausedEntered);
		Assert.True(pausedChildEntered);
		Assert.Equal(2, enteredPausedCount);
	}

	[Fact]
	public void Transition_direct_to_parent_state_exits_parents()
	{
		StateMachine<JukeBoxState, JukeBoxTrigger> sut = new(JukeBoxState.Playing);

		sut.Configure(JukeBoxState.Playing)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild);

		bool pausedExited = false;
		bool pausedChildExited = false;
		int exitedPausedCount = 0;

		sut.Configure(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Play, JukeBoxState.Playing)
			.OnExit(() =>
			{
				pausedExited = true;
				exitedPausedCount++;
			});

		sut.Configure(JukeBoxState.PausedChild)
			.SubstateOf(JukeBoxState.Paused)
			.OnExit(() =>
			{
				pausedChildExited = true;
				exitedPausedCount++;
			});

		sut.Start();

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(JukeBoxState.PausedChild, sut.State);
		Assert.True(sut.IsInState(JukeBoxState.Paused));

		sut.Fire(JukeBoxTrigger.Play);

		Assert.Equal(JukeBoxState.Playing, sut.State);
		Assert.False(sut.IsInState(JukeBoxState.Paused));
		Assert.False(sut.IsInState(JukeBoxState.PausedChild));
		Assert.True(pausedExited);
		Assert.True(pausedChildExited);
		Assert.Equal(2, exitedPausedCount);
	}

	[Fact]
	public void OnTriggers_Are_Handled_Before_Transitions()
	{
		StateMachine<JukeBoxState, JukeBoxTrigger> sut = new(JukeBoxState.Paused);

		sut.Configure(JukeBoxState.Playing)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild);

		bool onTriggerCalled = false;

		sut.Configure(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Play, JukeBoxState.Playing)
			.OnTrigger(JukeBoxTrigger.Play, () => onTriggerCalled = true);

		sut.Start();

		sut.Fire(JukeBoxTrigger.Play);

		Assert.Equal(JukeBoxState.Playing, sut.State);
		Assert.True(sut.IsInState(JukeBoxState.Playing));
		Assert.True(onTriggerCalled);
	}

	[Fact]
	public void OnEntry_Isnt_Called_For_Parent_State_If_We_Enter_Another_Child()
	{
		StateMachine<JukeBoxState, JukeBoxTrigger> sut = new(JukeBoxState.Paused);

		int entryCallCount = 0;

		sut.Configure(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild)
			.OnEntry(() => entryCallCount++);

		sut.Start();

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(1, entryCallCount);
	}

	[Fact]
	public void Is_In_State_Works_With_Child_Child_States()
	{
		StateMachine<JukeBoxState, JukeBoxTrigger> sut = new(JukeBoxState.Paused);

		int entryCallCount = 0;

		sut.Configure(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild);

		sut.Configure(JukeBoxState.PausedChild)
			.SubstateOf(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild2)
			.OnEntry(() => entryCallCount++);

		sut.Configure(JukeBoxState.PausedChild2)
			.SubstateOf(JukeBoxState.PausedChild)
			.OnEntry(() => entryCallCount++);

		sut.Start();

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(1, entryCallCount);
		Assert.True(sut.IsInState(JukeBoxState.Paused));

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(2, entryCallCount);
		Assert.True(sut.IsInState(JukeBoxState.Paused));
	}

	[Fact]
	public void Parent_Entry_Isnt_Called_Entering_Child_Child_States()
	{
		StateMachine<JukeBoxState, JukeBoxTrigger> sut = new(JukeBoxState.Paused);

		int entryCallCount = 0;
		int pausedEntryCount = 0;

		sut.Configure(JukeBoxState.Paused)
			.OnEntry(() => pausedEntryCount++)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild);

		sut.Configure(JukeBoxState.PausedChild)
			.SubstateOf(JukeBoxState.Paused)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild2)
			.OnEntry(() => entryCallCount++);

		sut.Configure(JukeBoxState.PausedChild2)
			.Permit(JukeBoxTrigger.Pause, JukeBoxState.PausedChild)
			.SubstateOf(JukeBoxState.PausedChild)
			.OnEntry(() => entryCallCount++);

		sut.Start();

		Assert.Equal(1, pausedEntryCount);

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(1, entryCallCount);

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(2, entryCallCount);

		sut.Fire(JukeBoxTrigger.Pause);

		Assert.Equal(1, pausedEntryCount);
		Assert.Equal(3, entryCallCount);
	}
}