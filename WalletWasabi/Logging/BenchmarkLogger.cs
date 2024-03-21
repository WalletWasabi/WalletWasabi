using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Logging;

public class BenchmarkLogger : IDisposable
{
	private bool _disposedValue = false; // To detect redundant calls

	private BenchmarkLogger(LogLevel logLevel, string operationName, string callerMemberName, string callerFilePath, int callerLineNumber)
	{
		LogLevel = logLevel;
		OperationName = operationName;
		CallerMemberName = callerMemberName;
		CallerFilePath = callerFilePath;
		CallerLineNumber = callerLineNumber;

		Stopwatch = Stopwatch.StartNew();
	}

	private LogLevel LogLevel { get; }
	public Stopwatch Stopwatch { get; }

	public string OperationName { get; }
	public string CallerMemberName { get; }
	public string CallerFilePath { get; }
	public int CallerLineNumber { get; }

	/// <summary>
	/// Logs the time between the creation of the class and the disposing of the class.
	/// Example usage: using (BenchmarkLogger.Measure()) { /* Your code here */ }
	/// </summary>
	/// <param name="operationName">Which operation to measure. Default is the caller function name.</param>
	public static IDisposable Measure(LogLevel logLevel = LogLevel.Info, string operationName = "", [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		if (operationName == "")
		{
			operationName = callerMemberName;
		}

		return new BenchmarkLogger(logLevel, operationName, callerMemberName, callerFilePath, callerLineNumber);
	}

	#region IDisposable Support

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				Stopwatch.Stop();

				double min = Stopwatch.Elapsed.TotalMinutes;
				double sec = Stopwatch.Elapsed.TotalSeconds;
				string message;
				if (min > 1)
				{
					message = $"{OperationName} finished in {min:#.##} minutes.";
				}
				else if (sec > 1)
				{
					message = $"{OperationName} finished in {sec:#.##} seconds.";
				}
				else
				{
					message = $"{OperationName} finished in {Stopwatch.ElapsedMilliseconds} milliseconds.";
				}

				Logger.Log(LogLevel, message, callerFilePath: CallerFilePath, callerMemberName: CallerMemberName, callerLineNumber: CallerLineNumber);
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
