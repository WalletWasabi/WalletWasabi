using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;

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
	private readonly Lock _lock = new();

	/// <remarks>Guarded by <see cref="_lock"/>.</remarks>
	protected IReadOnlyList<TaskCompletionSource<TEventArgs>> EventsArrived { get; }

	protected IReadOnlyList<Task<TEventArgs>> Tasks { get; }

	private readonly Action<EventHandler<TEventArgs>> _unsubscribe;

	private void SubscriptionEventHandler(object? sender, TEventArgs e)
	{
		if (e is SmartTransaction stx)
		{
			Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler was called; stx ({stx.GetHash()}): {stx}");
		}
		else
		{
			Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler was called; argument: {e}");
		}


		lock (_lock)
		{
			int i = 0;
			foreach (var task in Tasks)
			{
				i++;
				Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler - task #{i} has status: {task.Status}");
			}

			i = 0;
			foreach (var tcs in EventsArrived)
			{
				i++;
				Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler - TCS #{i} not completed  has status: {tcs.Task.Status} ({tcs.Task.IsCompleted})");
			}

			var firstUnfinished = EventsArrived.FirstOrDefault(x => !x.Task.IsCompleted);
			Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler - firstUnfinished={firstUnfinished}");

			firstUnfinished?.TrySetResult(e);

			// This is guaranteed to happen only once.
			if (Tasks.All(x => x.IsCompleted))
			{
				Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler - all tasks completed");
				_unsubscribe(SubscriptionEventHandler);
			}
			else
			{
				Console.WriteLine($"EventsAwaiter.SubscriptionEventHandler - not all tasks are completed");
			}
		}
	}

	public async Task<IEnumerable<TEventArgs>> WaitAsync(TimeSpan timeout)
		=> await Task.WhenAll(Tasks).WaitAsync(timeout).ConfigureAwait(false);
}
