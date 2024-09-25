using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Nito.AsyncEx;

/// <summary>
/// To remember tasks that were fired to forget and so wait for them during dispose.
/// </summary>
public class AbandonedTasks
{
	private readonly HashSet<Task> _tasks = new();
	private readonly object _lock = new();

	/// <summary>
	/// Adds tasks and clears completed ones atomically.
	/// </summary>
	public void AddAndClearCompleted(params Task[] tasks)
	{
		lock (_lock)
		{
			AddNoLock(tasks);

			ClearCompletedNoLock();
		}
	}

	/// <summary>Gets the number of outstanding tasks.</summary>
	/// <remarks>As a side-effect completed tasks are removed.</remarks>
	public int Count
	{
		get
		{
			lock (_lock)
			{
				ClearCompletedNoLock();
				return _tasks.Count;
			}
		}
	}

	/// <summary>
	/// Wait for all tasks to complete.
	/// </summary>
	public async Task WhenAllAsync()
	{
		do
		{
			Task[] tasks;
			lock (_lock)
			{
				// 1. Clear all the completed tasks.
				ClearCompletedNoLock();
				tasks = _tasks.ToArray();

				// 2. If all tasks cleared, then break.
				if (tasks.Length == 0)
				{
					break;
				}
			}

			// Save the task to have AggregatedExceptions.
			var whenAllTask = Task.WhenAll(tasks);

			// 3. Wait for all tasks to complete.
			try
			{
				await whenAllTask.ConfigureAwait(false);
			}
			catch (Exception)
			{
				if (whenAllTask.Exception is { } aggregatedException)
				{
					// Catch every exception but log only non-cancellation ones.
					foreach (var exc in aggregatedException.InnerExceptions.Where(ex => ex is not OperationCanceledException))
					{
						Logger.LogDebug(exc);
					}
				}
			}
		}
		while (true);
	}

	private void AddNoLock(params Task[] tasks)
	{
		foreach (var t in tasks)
		{
			_tasks.Add(t);
		}
	}

	private void ClearCompletedNoLock()
	{
		foreach (var t in _tasks.ToArray())
		{
			if (t.IsCompleted)
			{
				if (t.IsFaulted && t.Exception?.InnerException is { } exc)
				{
					Logger.LogDebug(exc);
				}

				_tasks.Remove(t);
			}
		}
	}
}
