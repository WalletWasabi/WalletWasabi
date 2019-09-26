using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace WalletWasabi.Logging
{
	public class BenchmarkLogger : IDisposable
	{
		private LogLevel LogLevel { get; }
		public DateTimeOffset InitStart { get; }

		public string OperationName { get; }
		public string CallerFilePath { get; }
		public int CallerLineNumber { get; }

		private BenchmarkLogger(LogLevel logLevel = LogLevel.Info, [CallerMemberName]string operationName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			LogLevel = logLevel;
			InitStart = DateTimeOffset.UtcNow;
			OperationName = operationName;
			CallerFilePath = callerFilePath;
			CallerLineNumber = callerLineNumber;
		}

		/// <summary>
		/// Logs the time between the creation of the class and the disposing of the class.
		/// Example usage: using(BenchmarkLogger.Measure()){}
		/// </summary>
		/// <param name="operationName">Which operation to measure. Default is the caller function name.</param>
		public static IDisposable Measure(LogLevel logLevel = LogLevel.Info, [CallerMemberName]string operationName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			return new BenchmarkLogger(logLevel, operationName, callerFilePath, callerLineNumber);
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
					string message = $"{OperationName} finished in {elapsedSeconds} seconds.";

					Logger.Log(LogLevel, message, CallerFilePath, CallerLineNumber);
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
