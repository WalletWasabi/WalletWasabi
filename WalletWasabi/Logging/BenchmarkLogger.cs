using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace WalletWasabi.Logging
{
	public class BenchmarkLogger : IDisposable
	{
		public DateTimeOffset InitStart { get; }

		public string CallerMemberName { get; }
		public string CallerFilePath { get; }
		public int CallerLineNumber { get; }

		private BenchmarkLogger([CallerMemberName]string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			InitStart = DateTimeOffset.UtcNow;
			CallerMemberName = callerMemberName;
			CallerFilePath = callerFilePath;
			CallerLineNumber = callerLineNumber;
		}

		public static IDisposable Measure()
		{
			return new BenchmarkLogger();
		}

		#region IDisposable Support

		private bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					var elapsedSeconds = Math.Round((DateTimeOffset.UtcNow - InitStart).TotalSeconds, 1);
					Logger.LogInfo($"{CallerMemberName} finished in {elapsedSeconds} seconds.", CallerFilePath, CallerLineNumber);
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
