using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services.Terminate
{
	public class TerminateService
	{
		private Func<Task> _terminateApplicationAsync;

		private const long TerminateStatusIdle = 0;
		private const long TerminateStatusInProgress = 1;
		private const long TerminateFinished = 2;

		private long _terminateStatus;

		public TerminateService(Func<Task> terminateApplicationAsync)
		{
			_terminateApplicationAsync = terminateApplicationAsync;
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			Console.CancelKeyPress += Console_CancelKeyPress;
		}

		private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
		{
			Logger.LogDebug("ProcessExit was called.");

			// This must be a blocking call because after this the OS will terminate Wasabi process if exists.
			Terminate();
		}

		private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		{
			Logger.LogWarning("Process was signaled for termination.");
			// This must be a blocking call because after this the OS will terminate Wasabi process if exists.
			// In some cases CurrentDomain_ProcessExit is called after this by the OS.
			Terminate();
		}

		/// <summary>
		/// This will terminate the application. This is a blocking method and no return after this call as it will exit the application.
		/// </summary>
		public void Terminate(int exitCode = 0)
		{
			var prevValue = Interlocked.CompareExchange(ref _terminateStatus, TerminateStatusInProgress, TerminateStatusIdle);
			if (prevValue != TerminateStatusIdle)
			{
				// Secondary callers will be blocked until the end of the termination.
				while (_terminateStatus != TerminateFinished)
				{
				}
				return;
			}

			// First caller starts the terminate procedure.
			Logger.LogDebug("Terminate application was started.");

			// Async termination has to be started on another thread otherwise there is a possibility of deadlock.
			// We still need to block the caller so ManualResetEvent applied.
			using ManualResetEvent resetEvent = new ManualResetEvent(false);
			Task.Run(async () => await _terminateApplicationAsync.Invoke().ContinueWith((arg) =>
			{
				if (arg?.Exception is { } ex)
				{
					Logger.LogWarning(ex.ToTypeMessageString());
				}

				resetEvent.Set();
			}));
			resetEvent.WaitOne();

			Dispose();

			// Indicate that the termination procedure finished. So other callers can return.
			Interlocked.Exchange(ref _terminateStatus, TerminateFinished);

			Environment.Exit(exitCode);
		}

		public bool IsTerminateRequested => Interlocked.Read(ref _terminateStatus) > TerminateStatusIdle;

		private void Dispose()
		{
			AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
			Console.CancelKeyPress -= Console_CancelKeyPress;
		}
	}
}
