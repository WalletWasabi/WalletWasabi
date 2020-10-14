using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services.SystemEventWatcher;

namespace WalletWasabi.Services.TerminateWatcher
{
	public class TerminateWatcher : IDisposable
	{
		// Do not use async here. The event has to be blocking in order to prevent the OS to terminate the application.
		public event TerminateEventHandlerDelegate? Terminate;

		public TerminateWatcher()
		{
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			Console.CancelKeyPress += Console_CancelKeyPress;
		}

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
