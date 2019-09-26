using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WalletWasabi.Logging
{
	public class BenchmarkLogger : IDisposable
	{
		private LogLevel LogLevel { get; }
		public Stopwatch Stopwatch { get; }

		public string OperationName { get; }
		public string CallerFilePath { get; }
		public int CallerLineNumber { get; }

		private BenchmarkLogger(LogLevel logLevel = LogLevel.Info, [CallerMemberName]string operationName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			LogLevel = logLevel;
			OperationName = operationName;
			CallerFilePath = callerFilePath;
			CallerLineNumber = callerLineNumber;

			Stopwatch = Stopwatch.StartNew();
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
					Stopwatch.Stop();

					var min = Stopwatch.Elapsed.TotalMinutes;
					var sec = Stopwatch.Elapsed.TotalSeconds;
					string message;
					if (min > 1)
					{
						message = $"{OperationName} finished in {(int)min} minutes.";
					}
					else if (sec > 1)
					{
						message = $"{OperationName} finished in {(int)sec} seconds.";
					}
					else
					{
						message = $"{OperationName} finished in {Stopwatch.ElapsedMilliseconds} milliseconds.";
					}

					Logger.Log(LogLevel, message, callerFilePath: CallerFilePath, callerLineNumber: CallerLineNumber);
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
