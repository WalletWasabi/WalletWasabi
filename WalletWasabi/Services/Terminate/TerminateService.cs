using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Services.Terminate
{
	public class TerminateService : IDisposable
	{
		private static long TerminateStatusIdle = 0;
		private static long TerminateStatusInProgress = 1;
		private static long TerminateStatusDone = 2;

		private long _terminateStatus = TerminateStatusIdle; // To detect redundant calls

		// Do not use async here. The event has to be blocking in order to prevent the OS to terminate the application.
		public event TerminateEventHandlerDelegate? Terminate;

		public TerminateService()
		{
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			Console.CancelKeyPress += Console_CancelKeyPress;
		}

		public async Task DoTerminateAsync()
		{
			var compareRes = Interlocked.CompareExchange(ref _terminateStatus, TerminateStatusInProgress, TerminateStatusIdle);
			if (compareRes == TerminateStatusInProgress)
			{
				while (Interlocked.Read(ref _terminateStatus) != TerminateStatusDone)
				{
					await Task.Delay(50).ConfigureAwait(false);
				}
				return;
			}
			else if (compareRes == TerminateStatusDone)
			{
				return;
			}

			Terminate?.Invoke(TerminateEventSourceEnum.Internal);

			Interlocked.Exchange(ref _terminateStatus, TerminateStatusDone);
		}

		public bool KillRequested => Interlocked.Read(ref _terminateStatus) > TerminateStatusIdle;

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			Terminate?.Invoke(TerminateEventSourceEnum.CancelKeyPress);
		}

		private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
		{
			Terminate?.Invoke(TerminateEventSourceEnum.ProcessExit);
		}

		#region Disposal

		private bool _disposedValue;

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

		#endregion Disposal
	}
}
