using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

		public Task<TEventArgs> Task => EventsArrived.First().Task;
	}
}
