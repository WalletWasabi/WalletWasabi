using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers;

/// <summary>
/// Source: https://stackoverflow.com/a/42117130/2061103
/// </summary>
public class EventsAwaiter<TEventArgs>
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private bool _isUnsubscribed;

	public EventsAwaiter(Action<EventHandler<TEventArgs>> subscriptionAction, Action<EventHandler<TEventArgs>> unsubscriptionAction, int count)
	{
		Guard.MinimumAndNotNull(nameof(count), count, smallest: 0);

		List<TaskCompletionSource<TEventArgs>> eventTcsList = new(count);

		for (int i = 0; i < count; i++)
		{
			eventTcsList.Add(new TaskCompletionSource<TEventArgs>());
		}

		EventTcsList = eventTcsList;
		Tasks = EventTcsList.Select(x => x.Task).ToArray();
		UnsubscriptionAction = unsubscriptionAction;

		subscriptionAction(SubscriptionEventHandler);
	}

	/// <remarks>Guards access to <see cref="_isUnsubscribed"/> and <see cref="EventTcsList"/>.</remarks>
	private object Lock { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	protected IReadOnlyList<TaskCompletionSource<TEventArgs>> EventTcsList { get; }
	protected IReadOnlyList<Task<TEventArgs>> Tasks { get; }

	private Action<EventHandler<TEventArgs>> UnsubscriptionAction { get; }

	private EventHandler<TEventArgs> SubscriptionEventHandler => (s, e) =>
	{
		lock (Lock)
		{
			TaskCompletionSource<TEventArgs>? firstUnfinished = EventTcsList.FirstOrDefault(x => !x.Task.IsCompleted);
			firstUnfinished?.TrySetResult(e);

			// Unsubscription action can be called just once.
			if (!_isUnsubscribed && Tasks.All(x => x.IsCompleted))
			{
				_isUnsubscribed = true;
				UnsubscriptionAction(SubscriptionEventHandler);
			}
		}
	};

	public async Task<IEnumerable<TEventArgs>> WaitAsync(TimeSpan timeout)
		=> await Task.WhenAll(Tasks).WithAwaitCancellationAsync(timeout).ConfigureAwait(false);

	public async Task<IEnumerable<TEventArgs>> WaitAsync(CancellationToken token)
		=> await Task.WhenAll(Tasks).WithAwaitCancellationAsync(token).ConfigureAwait(false);
}
