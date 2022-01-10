using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace System;

/// <summary>
/// Source: https://stackoverflow.com/a/42117130/2061103
/// </summary>
public class EventsAwaiter<TEventArgs>
{
	public EventsAwaiter(Action<EventHandler<TEventArgs>> subscribe, Action<EventHandler<TEventArgs>> unsubscribe, int count)
	{
		Guard.MinimumAndNotNull(nameof(count), count, 0);
		Lock = new object();
		var eventsArrived = new List<TaskCompletionSource<TEventArgs>>(count);
		for (int i = 0; i < count; i++)
		{
			eventsArrived.Add(new TaskCompletionSource<TEventArgs>());
		}
		EventsArrived = eventsArrived;
		subscribe(Subscription);
		Unsubscribe = unsubscribe;
	}

	protected IEnumerable<TaskCompletionSource<TEventArgs>> EventsArrived { get; }

	private Action<EventHandler<TEventArgs>> Unsubscribe { get; }
	private object Lock { get; }

	protected IEnumerable<Task<TEventArgs>> Tasks => EventsArrived.Select(x => x.Task);

	private EventHandler<TEventArgs> Subscription => (s, e) =>
	{
		lock (Lock)
		{
			var firstUnfinished = EventsArrived.FirstOrDefault(x => !x.Task.IsCompleted);
			firstUnfinished?.TrySetResult(e);

			if (EventsArrived.All(x => x.Task.IsCompleted))
			{
				Unsubscribe(Subscription);
			}
		}
	};

	public async Task<IEnumerable<TEventArgs>> WaitAsync(TimeSpan timeout)
		=> await Task.WhenAll(Tasks).WithAwaitCancellationAsync(timeout).ConfigureAwait(false);
}
