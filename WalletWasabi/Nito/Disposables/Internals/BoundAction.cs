using System;
using System.Threading;

namespace Nito.Disposables.Internals
{
	/// <summary>
	/// A field containing a bound action.
	/// </summary>
	/// <typeparam name="T">The type of context for the action.</typeparam>
	public sealed class BoundActionField<T>
	{
		private BoundAction _field;

		/// <summary>
		/// Initializes the field with the specified action and context.
		/// </summary>
		/// <param name="action">The action delegate.</param>
		/// <param name="context">The context.</param>
		public BoundActionField(Action<T> action, T context)
		{
			_field = new BoundAction(action, context);
		}

		/// <summary>
		/// Whether the field is empty.
		/// </summary>
		public bool IsEmpty => Interlocked.CompareExchange(ref _field, null, null) is null;

		/// <summary>
		/// Atomically retrieves the bound action from the field and sets the field to <c>null</c>. May return <c>null</c>.
		/// </summary>
		public IBoundAction TryGetAndUnset()
		{
			return Interlocked.Exchange(ref _field, null);
		}

		/// <summary>
		/// Attempts to update the context of the bound action stored in the field. Returns <c>false</c> if the field is <c>null</c>.
		/// </summary>
		/// <param name="contextUpdater">The function used to update an existing context. This may be called more than once if more than one thread attempts to simultanously update the context.</param>
		public bool TryUpdateContext(Func<T, T> contextUpdater)
		{
			while (true)
			{
				var original = Interlocked.CompareExchange(ref _field, _field, _field);
				if (original is null)
				{
					return false;
				}

				var updatedContext = new BoundAction(original, contextUpdater);
				var result = Interlocked.CompareExchange(ref _field, updatedContext, original);
				if (ReferenceEquals(original, result))
				{
					return true;
				}
			}
		}

		/// <summary>
		/// An action delegate bound with its context.
		/// </summary>
		public interface IBoundAction
		{
			/// <summary>
			/// Executes the action. This should only be done after the bound action is retrieved from a field by <see cref="TryGetAndUnset"/>.
			/// </summary>
			void Invoke();
		}

		private sealed class BoundAction : IBoundAction
		{
			private readonly Action<T> Action;
			private readonly T Context;

			public BoundAction(Action<T> action, T context)
			{
				Action = action;
				Context = context;
			}

			public BoundAction(BoundAction originalBoundAction, Func<T, T> contextUpdater)
			{
				Action = originalBoundAction.Action;
				Context = contextUpdater(originalBoundAction.Context);
			}

			public void Invoke()
			{
				Action?.Invoke(Context);
			}
		}
	}
}
