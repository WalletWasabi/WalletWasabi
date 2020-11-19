using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Nito.AsyncEx
{
	/// <summary>
	/// To remember tasks those were fired to forget and so wait for them during dispose.
	/// </summary>
	public class TaskRemembler
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
				await Task.WhenAll(tasks).ConfigureAwait(false);
			} while (true);
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
			var toRemove = new List<Task>();

			foreach (var t in Tasks)
			{
				if (t.IsCompleted)
				{
					toRemove.Add(t);
				}
			}

			foreach (var t in toRemove)
			{
				Tasks.Remove(t);
			}
		}
	}
}
