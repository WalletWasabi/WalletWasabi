using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers;

/// <summary>
/// Source: https://stackoverflow.com/a/42117130/2061103
/// </summary>
public class EventAwaiter<TEventArgs> : EventsAwaiter<TEventArgs>
{
	public EventAwaiter(Action<EventHandler<TEventArgs>> subscribe, Action<EventHandler<TEventArgs>> unsubscribe) : base(subscribe, unsubscribe, count: 1)
	{
		Task = Tasks[0];
	}

	protected Task<TEventArgs> Task { get; }

	public new async Task<TEventArgs> WaitAsync(TimeSpan timeout)
		=> await Task.WaitAsync(timeout).ConfigureAwait(false);

	public async Task<TEventArgs> WaitAsync(CancellationToken token)
		=> await Task.WaitAsync(token).ConfigureAwait(false);
}
