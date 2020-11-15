using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services.Terminate
{
	public class TerminateService
	{
		private const long TerminateStatusNotStarted = 0;
		private const long TerminateStatusInProgress = 1;
		private const long TerminateStatusFinished = 2;
		private readonly Func<Task> _terminateApplicationAsync;
		private long _terminateStatus;

		public TerminateService(Func<Task> terminateApplicationAsync)
		{
			_terminateApplicationAsync = terminateApplicationAsync;
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			Console.CancelKeyPress += Console_CancelKeyPress;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				SystemEvents.SessionEnding += Windows_SystemEvents_SessionEnding;
			}
		}

		public bool IsTerminateRequested => Interlocked.Read(ref _terminateStatus) > TerminateStatusNotStarted;

		private void Windows_SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
		{
			// This event will only be triggered if you run Wasabi from the published package. Use the packager with the --onlybinaries option.
			Logger.LogInfo($"Process termination was requested by the OS, reason '{e.Reason}'.");
			e.Cancel = true;

			// This must be a blocking call because after this the OS will terminate the Wasabi process if it exists.
			// The process will be killed by the OS after ~7 seconds, even with e.Cancel = true.
			Terminate();
		}

		private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
		{
			Logger.LogDebug("ProcessExit was called.");

			// This must be a blocking call because after this the OS will terminate Wasabi process if exists.
			Terminate();
		}

		private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		{
			Logger.LogWarning($"Process termination was requested using '{e.SpecialKey}' keyboard shortcut.");

			// This must be a blocking call because after this the OS will terminate Wasabi process if it exists.
			// In some cases CurrentDomain_ProcessExit is called after this by the OS.
			Terminate();
		}

		/// <summary>
		/// Terminates the application.
		/// </summary>
		/// <remark>This is a blocking method. Note that program execution ends at the end of this method due to <see cref="Environment.Exit(int)"/> call.</remark>
		public void Terminate(int exitCode = 0)
		{
			var prevValue = Interlocked.CompareExchange(ref _terminateStatus, TerminateStatusInProgress, TerminateStatusNotStarted);
			Logger.LogTrace($"Terminate was called from ThreadId: {Thread.CurrentThread.ManagedThreadId}");
			if (prevValue != TerminateStatusNotStarted)
			{
				// Secondary callers will be blocked until the end of the termination.
				while (Interlocked.Read(ref _terminateStatus) != TerminateStatusFinished)
				{
					Thread.Sleep(50);
				}
				return;
			}

			// First caller starts the terminate procedure.
			Logger.LogDebug("Start shutting down the application.");

			// Async termination has to be started on another thread otherwise there is a possibility of deadlock.
			// We still need to block the caller so Wait applied.
			Task.Run(async () =>
			{
				try
				{
					await _terminateApplicationAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex.ToTypeMessageString());
				}
			}).Wait();

			AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
			Console.CancelKeyPress -= Console_CancelKeyPress;
			SystemEvents.SessionEnding -= Windows_SystemEvents_SessionEnding;

			// Indicate that the termination procedure finished. So other callers can return.
			Interlocked.Exchange(ref _terminateStatus, TerminateStatusFinished);

			Logger.LogSoftwareStopped("Wasabi");

			Environment.Exit(exitCode);
		}
	}
}
