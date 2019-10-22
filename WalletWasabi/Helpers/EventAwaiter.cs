using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace System
{
	/// <summary>
	/// https://stackoverflow.com/a/42117130/2061103
	/// </summary>
	public class EventAwaiter<TEventArgs>
	{
		private TaskCompletionSource<TEventArgs> EventArrived { get; }

		private readonly Action<EventHandler<TEventArgs>> Unsubscribe;

		public EventAwaiter(Action<EventHandler<TEventArgs>> subscribe, Action<EventHandler<TEventArgs>> unsubscribe)
		{
			EventArrived = new TaskCompletionSource<TEventArgs>();
			subscribe(Subscription);
			Unsubscribe = unsubscribe;
		}

		public Task<TEventArgs> Task => EventArrived.Task;

		private EventHandler<TEventArgs> Subscription => (s, e) =>
		{
			EventArrived.TrySetResult(e);
			Unsubscribe(Subscription);
		};
	}
}
