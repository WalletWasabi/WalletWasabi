using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Microservices
{
	/// <summary>
	/// Async wrapper class for <see cref="System.Diagnostics.Process"/> class that implements <see cref="WaitForExitAsync(CancellationToken, bool)"/>
	/// to asynchronously wait for a process to exit.
	/// </summary>
	/// <remarks><see cref="IDisposable"/> is implemented. Do not forget to use `using` or dispose any instance of this class.</remarks>
	public class ProcessAsync : IDisposable
	{
		/// <summary>
		/// To detect redundant calls.
		/// </summary>
		private bool _disposed = false;

		public ProcessAsync(ProcessStartInfo startInfo) : this(new Process() { StartInfo = startInfo })
		{
		}

		private ProcessAsync(Process process)
		{
			ProcessExecutionTcs = new TaskCompletionSource<bool>();

			Process = process;
			Process.EnableRaisingEvents = true;
			Process.Exited += OnExited;
		}

		private TaskCompletionSource<bool> ProcessExecutionTcs { get; }

		private Process Process { get; }

		/// <inheritdoc cref="Process.StartInfo"/>
		public ProcessStartInfo StartInfo => Process.StartInfo;

		/// <inheritdoc cref="Process.ExitCode"/>
		public int ExitCode => Process.ExitCode;

		/// <inheritdoc cref="Process.HasExited"/>
		public bool HasExited => Process.HasExited;

		/// <inheritdoc cref="Process.Id"/>
		public int Id => Process.Id;

		/// <inheritdoc cref="Process.StandardInput"/>
		public StreamWriter StandardInput => Process.StandardInput;

		/// <inheritdoc cref="Process.StandardOutput"/>
		public StreamReader StandardOutput => Process.StandardOutput;

		/// <inheritdoc cref="Process.Start()"/>
		public void Start()
		{
			try
			{
				Process.Start();
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);

				Logger.LogInfo($"{nameof(Process.StartInfo.FileName)}: {Process.StartInfo.FileName}.");
				Logger.LogInfo($"{nameof(Process.StartInfo.Arguments)}: {Process.StartInfo.Arguments}.");
				Logger.LogInfo($"{nameof(Process.StartInfo.RedirectStandardOutput)}: {Process.StartInfo.RedirectStandardOutput}.");
				Logger.LogInfo($"{nameof(Process.StartInfo.UseShellExecute)}: {Process.StartInfo.UseShellExecute}.");
				Logger.LogInfo($"{nameof(Process.StartInfo.CreateNoWindow)}: {Process.StartInfo.CreateNoWindow}.");
				Logger.LogInfo($"{nameof(Process.StartInfo.WindowStyle)}: {Process.StartInfo.WindowStyle}.");
				throw;
			}
		}

		/// <inheritdoc cref="Process.Kill()"/>
		public void Kill()
		{
			Process.Kill();
		}

		/// <inheritdoc cref="Process.Kill(bool)"/>
		public void Kill(bool entireProcessTree)
		{
			Process.Kill(entireProcessTree);
		}

		/// <summary>
		/// Waits until the process either finishes on its own or when user cancels the action.
		/// </summary>
		/// <param name="cancel">Cancellation token.</param>
		/// <param name="killOnCancel">If <c>true</c> the process will be killed (with entire process tree) when this asynchronous action is canceled via <paramref name="cancel"/> token.</param>
		/// <returns><see cref="Task"/>.</returns>
		public async Task WaitForExitAsync(CancellationToken cancel, bool killOnCancel = false)
		{
			if (Process.HasExited)
			{
				return;
			}

			try
			{
				// If this token is already in the canceled state, the delegate will be run immediately and synchronously.
				using (cancel.Register(() => ProcessExecutionTcs.TrySetCanceled()))
				{
					await ProcessExecutionTcs.Task;
				}
			}
			catch (OperationCanceledException ex)
			{
				if (killOnCancel)
				{
					if (!Process.HasExited)
					{
						try
						{
							Process.Kill(entireProcessTree: true);
						}
						catch (Exception e)
						{
							Logger.LogError($"Could not kill process: {e}.");
						}
					}
				}

				throw new TaskCanceledException("Waiting for process exiting was canceled.", ex, cancel);
			}
		}

		// Protected implementation of Dispose pattern.
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects).
				Process.Exited -= OnExited;
				Process.Dispose();
			}

			_disposed = true;
		}

		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);

			// Suppress finalization.
			GC.SuppressFinalize(this);
		}

		private void OnExited(object? sender, EventArgs? e)
		{
			ProcessExecutionTcs.TrySetResult(true);
		}
	}
}
