using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace System.Diagnostics
{
	public static class ProcessExtensions
	{
		private static Dictionary<int, TaskCompletionSource<bool>> AwaitingTaskCompletionSources { get; } = new Dictionary<int, TaskCompletionSource<bool>>();
		private static object AwaitingTaskCompletionSourcesLock { get; } = new object();

		public static async Task WaitForExitAsync(this Process process, CancellationToken cancel)
		{
			if (process.HasExited)
			{
				return;
			}

			// https://stackoverflow.com/a/12858633
			var tcs = new TaskCompletionSource<bool>();
			try
			{
				lock (AwaitingTaskCompletionSourcesLock)
				{
					AwaitingTaskCompletionSources.Add(process.Id, tcs);
					process.EnableRaisingEvents = true;
					process.Exited += Process_Exited;
				}

				try
				{
					await tcs.Task.WithAwaitCancellationAsync(cancel).ConfigureAwait(false);
				}
				catch (OperationCanceledException ex)
				{
					if (!process.HasExited) // Final check.
					{
						// Originally it was TaskCanceledException here, so I don't want to change the behavior.
						throw new TaskCanceledException("Waiting for process exiting was canceled.", ex, cancel);
					}
				}
			}
			finally
			{
				lock (AwaitingTaskCompletionSourcesLock)
				{
					process.Exited -= Process_Exited;
					AwaitingTaskCompletionSources.Remove(process.Id);
				}
			}
		}

		private static void Process_Exited(object sender, EventArgs e)
		{
			try
			{
				lock (AwaitingTaskCompletionSourcesLock)
				{
					var proc = sender as Process;
					TaskCompletionSource<bool> tcs = AwaitingTaskCompletionSources[proc.Id];
					tcs.SetResult(true);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, nameof(ProcessExtensions));
			}
		}
	}
}
