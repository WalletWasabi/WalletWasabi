using System.Collections.Generic;

namespace WalletWasabi.Fluent.State;

/// <summary>
/// StateMachine - api based on: https://github.com/dotnet-state-machine/stateless
/// </summary>
public class StateMachine<TState, TTrigger> where TTrigger : Enum where TState : struct, Enum
{
	private StateContext _currentState;
	private readonly Dictionary<TState, StateContext> _states;
	private OnTransitionedDelegate? _onTransitioned;

	public delegate void OnTransitionedDelegate(TState from, TState to);

	public TState State => _currentState.StateId;

	public bool IsInState(TState state)
	{
		return IsAncestorOfInclusive(_currentState.StateId, state);
	}

	public StateMachine(TState initialState)
	{
		_states = new Dictionary<TState, StateContext>();

		RegisterStates();

		_currentState = Configure(initialState);
	}

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

	private bool IsAncestorOfExclusive(TState state, TState parent)
	{
		if (state.Equals(parent))
		{
			return false;
		}

		return IsAncestorOfInclusive(state, parent);
	}


	private bool IsAncestorOfInclusive(TState state, TState parent)
	{
		if (_states.ContainsKey(state))
		{
			StateContext current = _states[state];

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

		if (_currentState.GetDestination(trigger) is { } destination)
		{
			Goto(destination);
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

	private void Goto(TState destination)
	{
		var origin = _currentState.StateId;

		if (_states.ContainsKey(destination))
		{
			StateContext ExitStates(StateContext current)
			{
				if (!IsAncestorOfInclusive(destination, current.StateId))
				{
					current.Exit();
				}

				if (current.Parent is { } parent)
				{
					ExitStates(parent);
				}

				return current;
			}

			StateContext EnterStates(StateContext current)
			{
				if (current.Parent is { } parent)
				{
					if (!IsAncestorOfInclusive(origin, parent.StateId))
					{
						EnterStates(parent);
					}
				}

				current.Enter();

				return current;
			}

			_currentState = ExitStates(_currentState);

			_currentState = EnterStates(_states[destination]);

			_onTransitioned?.Invoke(origin, _currentState.StateId);
		}
	}

	public class StateContext
	{
		private readonly Dictionary<TTrigger, TState> _permittedTransitions;
		private readonly StateMachine<TState, TTrigger> _owner;
		private readonly List<Action> _entryActions;
		private readonly List<Action> _exitActions;
		private readonly Dictionary<TTrigger, List<Action>> _triggerActions;

		public TState StateId { get; }

		public StateContext? Parent { get; private set; }

		internal TState? InitialTransitionTo { get; private set; }

		public StateContext(StateMachine<TState, TTrigger> owner, TState state)
		{
			_owner = owner;
			StateId = state;

			_entryActions = new();
			_exitActions = new();
			_triggerActions = new();
			_permittedTransitions = new();
		}

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
			if (_triggerActions.ContainsKey(trigger) && _triggerActions[trigger] is { } actions)
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

		internal TState? GetDestination(TTrigger trigger)
		{
			StateContext current = this;

			while (true)
			{
				if (current._permittedTransitions.ContainsKey(trigger))
				{
					return current._permittedTransitions[trigger];
				}

				if (current.Parent is { })
				{
					current = current.Parent;
				}
				else
				{
					return null;
				}
			}
		}
	}
}
