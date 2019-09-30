using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace System.Diagnostics
{
	public static class ProcessExtensions
	{
		private static Dictionary<int, TaskCompletionSource<bool>> AwaitingTaskCompletitionSources { get; } = new Dictionary<int, TaskCompletionSource<bool>>();

		public static async Task WaitForExitAsync(this Process process, CancellationToken cancel)
		{
			// https://stackoverflow.com/a/12858633
			var tcs = new TaskCompletionSource<bool>();
			try
			{
				AwaitingTaskCompletitionSources.Add(process.Id, tcs);
				process.EnableRaisingEvents = true;
				process.Exited += Process_Exited;

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
				process.Exited -= Process_Exited;
				AwaitingTaskCompletitionSources.Remove(process.Id);
			}
		}

		private static void Process_Exited(object sender, EventArgs e)
		{
			try
			{
				var proc = sender as Process;
				TaskCompletionSource<bool> tcs = AwaitingTaskCompletitionSources[proc.Id];
				tcs.SetResult(true);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, nameof(ProcessExtensions));
			}
		}
	}
}
