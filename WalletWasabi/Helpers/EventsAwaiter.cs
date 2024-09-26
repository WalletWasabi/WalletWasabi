using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers;

/// <summary>
/// Source: https://stackoverflow.com/a/42117130/2061103
/// </summary>
public class EventsAwaiter<TEventArgs>
{
	public EventsAwaiter(Action<EventHandler<TEventArgs>> subscribe, Action<EventHandler<TEventArgs>> unsubscribe, int count)
	{
		Guard.MinimumAndNotNull(nameof(count), count, smallest: 0);

		var eventsArrived = new List<TaskCompletionSource<TEventArgs>>(count);

		for (int i = 0; i < count; i++)
		{
			eventsArrived.Add(new TaskCompletionSource<TEventArgs>());
		}

		EventsArrived = eventsArrived;
		Tasks = EventsArrived.Select(x => x.Task).ToArray();
		_unsubscribe = unsubscribe;

		subscribe(SubscriptionEventHandler);
	}

	/// <remarks>Guards <see cref="EventsArrived"/>.</remarks>
	private readonly object _lock = new();

	/// <remarks>Guarded by <see cref="_lock"/>.</remarks>
	protected IReadOnlyList<TaskCompletionSource<TEventArgs>> EventsArrived { get; }

	protected IReadOnlyList<Task<TEventArgs>> Tasks { get; }

	private readonly Action<EventHandler<TEventArgs>> _unsubscribe;

	private void SubscriptionEventHandler(object? sender, TEventArgs e)
	{
		lock (_lock)
		{
			var firstUnfinished = EventsArrived.FirstOrDefault(x => !x.Task.IsCompleted);
			firstUnfinished?.TrySetResult(e);

			// This is guaranteed to happen only once.
			if (Tasks.All(x => x.IsCompleted))
			{
				_unsubscribe(SubscriptionEventHandler);
			}
		}
	}

	public async Task<IEnumerable<TEventArgs>> WaitAsync(TimeSpan timeout)
		=> await Task.WhenAll(Tasks).WaitAsync(timeout).ConfigureAwait(false);
}
