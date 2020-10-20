using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services.Terminate
{
	public class TerminateService : IDisposable
	{
		private bool _disposedValue;
		private Func<Exception?, Task> _terminateApplication;

		public TerminateService(Func<Exception?, Task> terminateApplication)
		{
			_terminateApplication = terminateApplication;
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			Console.CancelKeyPress += Console_CancelKeyPress;
		}

		private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
		{
			Logger.LogDebug("ProcessExit was called.");

			// This must be a blocking call because after this the OS will terminate Wasabi process if exists.
			Terminate();
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			Logger.LogWarning("Process was signaled for termination.");
			// This must be a blocking call because after this the OS will terminate Wasabi process if exists.
			// In some cases CurrentDomain_ProcessExit is called after this by the OS.
			Terminate();
		}

		private const long TerminateStatusIdle = 0;
		private const long TerminateStatusInProgress = 1;
		private const long TerminateFinished = 2;

		private long _terminateStatus;

		/// <summary>
		/// This will terminate the application. This is a blocking method and no return after this call as it will exit the application.
		/// </summary>
		/// <param name="criticalException"></param>
		public void Terminate(Exception? criticalException = null)
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
			// We still need to block the caller so
			using ManualResetEvent resetEvent = new ManualResetEvent(false);
			Task.Run(async () => await _terminateApplication.Invoke(criticalException).ContinueWith((ex) => resetEvent.Set()));
			resetEvent.WaitOne();

			Interlocked.Exchange(ref _terminateStatus, TerminateFinished);

			Environment.Exit(criticalException is { } ? 1 : 0);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
					Console.CancelKeyPress -= Console_CancelKeyPress;
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
