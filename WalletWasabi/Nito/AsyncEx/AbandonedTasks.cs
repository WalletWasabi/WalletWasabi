using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Nito.AsyncEx
{
	/// <summary>
	/// To remember tasks those were fired to forget and so wait for them during dispose.
	/// </summary>
	public class AbandonedTasks
	{
		private HashSet<Task> Tasks { get; } = new HashSet<Task>();
		private object Lock { get; } = new object();

		/// <summary>
		/// Adds tasks and clears completed ones atomically.
		/// </summary>
		public void AddAndClearCompleted(params Task[] tasks)
		{
			lock (Lock)
			{
				AddNoLock(tasks);

				ClearCompletedNoLock();
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
				lock (Lock)
				{
					// 1. Clear all the completed tasks.
					ClearCompletedNoLock();
					tasks = Tasks.ToArray();

					// 2. If all tasks cleared, then break.
					if (!tasks.Any())
					{
						break;
					}
				}

				// 3. Wait for all tasks to complete.
				try
				{
					await Task.WhenAll(tasks).ConfigureAwait(false);
				}
				catch (Exception exc)
				{
					// Catch every exceptions but log only non-cancellation ones.
					if (!(exc is OperationCanceledException))
					{
						Logger.LogDebug(exc);
					}
				}
			}
			while (true);
		}

		private void AddNoLock(params Task[] tasks)
		{
			foreach (var t in tasks)
			{
				Tasks.Add(t);
			}
		}

		private void ClearCompletedNoLock()
		{
			foreach (var t in Tasks.ToArray())
			{
				if (t.IsCompleted)
				{
					if (t.IsFaulted && t.Exception?.InnerException is { } exc)
					{
						Logger.LogDebug(exc);
					}

					Tasks.Remove(t);
				}
			}
		}
	}
}
