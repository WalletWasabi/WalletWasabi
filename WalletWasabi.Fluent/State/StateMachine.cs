using System.Collections.Generic;

namespace WalletWasabi.Fluent.State;

/// <summary>
/// StateMachine - api based on: https://github.com/dotnet-state-machine/stateless
/// </summary>
public class StateMachine<TState, TTrigger> where TTrigger : Enum where TState : struct, Enum
{
	private readonly Dictionary<TState, StateContext> _states;
	private StateContext _currentState;
	private OnTransitionedDelegate? _onTransitioned;

	public bool IsInState(TState state)
	{
		return IsAncestorOf(_currentState.StateId, state);
	}

	public StateMachine(TState initialState)
	{
		_states = new Dictionary<TState, StateContext>();

		RegisterStates();

		_currentState = Configure(initialState);
	}

	public delegate void OnTransitionedDelegate(TState from, TState to);

	public TState State => _currentState.StateId;

	private void RegisterStates()
	{
		foreach (var state in Enum.GetValues<TState>())
		{
			_states.Add(state, new StateContext(this, state));
		}
	}

	public StateMachine<TState, TTrigger> OnTransitioned(OnTransitionedDelegate onTransitioned)
	{
		_onTransitioned = onTransitioned;

		return this;
	}

	private bool IsAncestorOf(TState state, TState parent)
	{
		if (_states.TryGetValue(state, out StateMachine<TState, TTrigger>.StateContext? value))
		{
			StateContext current = value;

			while (true)
			{
				if (current.StateId.Equals(parent))
				{
					return true;
				}

				if (current.Parent is { })
				{
					current = current.Parent;
				}
				else
				{
					return false;
				}
			}
		}

		return false;
	}

	public StateContext Configure(TState state)
	{
		return _states[state];
	}

	public void Fire(TTrigger trigger)
	{
		_currentState.Process(trigger);

		if (_currentState.CanTransit(trigger))
		{
			var destination = _currentState.GetDestination(trigger);

			if (_states.TryGetValue(destination, out StateMachine<TState, TTrigger>.StateContext? value) && value.Parent is { } parent && !IsInState(parent.StateId))
			{
				Goto(parent.StateId);
			}

			Goto(destination);
		}
		else if (_currentState.Parent is { } && _currentState.Parent.CanTransit(trigger))
		{
			Goto(_currentState.Parent.StateId, true, false);
			Goto(_currentState.GetDestination(trigger));
		}
	}

	public void Start()
	{
		Enter();
	}

	private void Enter()
	{
		_currentState.Enter();

		if (_currentState.InitialTransitionTo is { } state)
		{
			Goto(state);
		}
	}

	private void Goto(TState state, bool exit = true, bool enter = true)
	{
		if (_states.TryGetValue(state, out StateMachine<TState, TTrigger>.StateContext? value))
		{
			if (exit && !IsAncestorOf(state, _currentState.StateId))
			{
				_currentState.Exit();
			}

			var old = _currentState.StateId;

			_currentState = value;

			_onTransitioned?.Invoke(old, _currentState.StateId);

			if (enter)
			{
				Enter();
			}
		}
	}

	public class StateContext
	{
		private readonly Dictionary<TTrigger, TState> _permittedTransitions;
		private readonly StateMachine<TState, TTrigger> _owner;
		private readonly List<Action> _entryActions;
		private readonly List<Action> _exitActions;
		private readonly Dictionary<TTrigger, List<Action>> _triggerActions;

		public StateContext(StateMachine<TState, TTrigger> owner, TState state)
		{
			_owner = owner;
			StateId = state;

			_entryActions = new();
			_exitActions = new();
			_triggerActions = new();
			_permittedTransitions = new();
		}

		public TState StateId { get; }

		public StateContext? Parent { get; private set; }

		internal TState? InitialTransitionTo { get; private set; }

		public StateContext InitialTransition(TState? state)
		{
			InitialTransitionTo = state;

			return this;
		}

		public StateContext SubstateOf(TState parent)
		{
			Parent = _owner._states[parent];

			return this;
		}

		public StateContext Permit(TTrigger trigger, TState state)
		{
			if (StateId.Equals(state))
			{
				throw new InvalidOperationException("Configuring state re-entry is not allowed.");
			}

			_permittedTransitions[trigger] = state;

			return this;
		}

		public StateContext OnEntry(Action action)
		{
			_entryActions.Add(action);

			return this;
		}

		public StateContext Custom(Func<StateContext, StateContext> custom)
		{
			return custom(this);
		}

		public StateContext OnTrigger(TTrigger trigger, Action action)
		{
			if (_triggerActions.TryGetValue(trigger, out var t))
			{
				t.Add(action);
			}
			else
			{
				_triggerActions.Add(trigger, new List<Action> { action });
			}

			return this;
		}

		public StateContext OnExit(Action action)
		{
			_exitActions.Add(action);

			return this;
		}

		internal void Enter()
		{
			foreach (var action in _entryActions)
			{
				action();
			}
		}

		internal void Exit()
		{
			foreach (var action in _exitActions)
			{
				action();
			}
		}

		internal void Process(TTrigger trigger)
		{
			if (_triggerActions.TryGetValue(trigger, out List<Action>? value) && value is { } actions)
			{
				foreach (var action in actions)
				{
					action();
				}
			}

			if (Parent is { })
			{
				Parent.Process(trigger);
			}
		}

		internal bool CanTransit(TTrigger trigger)
		{
			return _permittedTransitions.ContainsKey(trigger);
		}

		internal TState GetDestination(TTrigger trigger)
		{
			return _permittedTransitions[trigger];
		}
	}
}
