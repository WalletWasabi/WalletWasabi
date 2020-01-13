using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System
{
	/// <summary>
	/// https://stackoverflow.com/a/42117130/2061103
	/// </summary>
	public class EventAwaiter<TEventArgs> : EventsAwaiter<TEventArgs>
	{
		public EventAwaiter(Action<EventHandler<TEventArgs>> subscribe, Action<EventHandler<TEventArgs>> unsubscribe) : base(subscribe, unsubscribe, 1)
		{
		}

		protected Task<TEventArgs> Task => EventsArrived.First().Task;

		public new async Task<TEventArgs> WaitAsync(TimeSpan timeout)
			=> await Task.WithAwaitCancellationAsync(timeout).ConfigureAwait(false);

		public new async Task<TEventArgs> WaitAsync(int millisecondsTimeout)
			=> await Task.WithAwaitCancellationAsync(millisecondsTimeout).ConfigureAwait(false);

		public new async Task<TEventArgs> WaitAsync(CancellationToken cancel, int waitForGracefulTerminationMilliseconds = 0)
			=> await Task.WithAwaitCancellationAsync(cancel, waitForGracefulTerminationMilliseconds).ConfigureAwait(false);
	}
}
