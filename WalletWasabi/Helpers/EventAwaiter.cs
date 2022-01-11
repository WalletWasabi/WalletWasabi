using System.Linq;
using System.Threading.Tasks;

namespace System;

/// <summary>
/// Source: https://stackoverflow.com/a/42117130/2061103
/// </summary>
public class EventAwaiter<TEventArgs> : EventsAwaiter<TEventArgs>
{
	public EventAwaiter(Action<EventHandler<TEventArgs>> subscribe, Action<EventHandler<TEventArgs>> unsubscribe) : base(subscribe, unsubscribe, 1)
	{
	}

	protected Task<TEventArgs> Task => EventsArrived.First().Task;

	public new async Task<TEventArgs> WaitAsync(TimeSpan timeout)
		=> await Task.WithAwaitCancellationAsync(timeout).ConfigureAwait(false);
}
