using System;
using Microsoft.Extensions.Logging;

namespace WalletWasabi.Backend.Logging
{
	public class WabiSabiLogger : ILogger
	{
		private readonly string _name;

		public WabiSabiLogger(string name)
		{
			_name = name;
		}

		public IDisposable BeginScope<TState>(TState state) => default;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception exception,
			Func<TState, Exception, string> formatter)
		{
			var wasabiLogLevel = logLevel switch
			{
				LogLevel.None => WalletWasabi.Logging.LogLevel.Info,
				LogLevel.Information => WalletWasabi.Logging.LogLevel.Info,
				LogLevel.Trace => WalletWasabi.Logging.LogLevel.Trace,
				LogLevel.Warning => WalletWasabi.Logging.LogLevel.Warning,
				LogLevel.Debug => WalletWasabi.Logging.LogLevel.Debug,
				LogLevel.Error => WalletWasabi.Logging.LogLevel.Error,
				LogLevel.Critical => WalletWasabi.Logging.LogLevel.Critical,
				_ => throw new NotSupportedException($"Unknown {logLevel} level.")
			};

			WalletWasabi.Logging.Logger.Log(wasabiLogLevel, $"{_name} - {formatter(state, exception)}", 0, false, "", "", -1);
		}
	}
}
