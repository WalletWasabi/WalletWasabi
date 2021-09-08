using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WalletWasabi.Backend.Logging
{
	public class WabiSabiLogger : ILogger
	{
		private const string LoglevelPadding = ": ";
		private static readonly string MessagePadding = new string(' ', 6);
		private static readonly string NewLineWithMessagePadding = Environment.NewLine + MessagePadding;

		private readonly string _name;
		private readonly Func<object, string> _scopeFormatter;
		private readonly WabiSabiLoggerProvider _loggerProvider;

		public WabiSabiLogger(WabiSabiLoggerProvider loggerProvider, string name, Func<object, string> formatter)
		{
			_name = name;
			_scopeFormatter = formatter;
			_loggerProvider = loggerProvider;
		}

		public IDisposable BeginScope<TState>(TState state)
		{
			return _loggerProvider.ScopeProvider.Push(state);
		}

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

			var sb = new StringBuilder();
			sb.Append(_name).Append(' ');
			sb.Append('[').Append(eventId).Append("]: ");
			sb.Append(_scopeFormatter(state /*, exception*/)).AppendLine();
			AppendScopeInfo(sb);
			WalletWasabi.Logging.Logger.Log(wasabiLogLevel, sb.ToString(), 0, false, "", "", -1);
		}

		private bool AppendScopeInfo(StringBuilder stringBuilder)
		{
			if (_loggerProvider.ScopeProvider is { } scopeProvider)
			{
				var initialLength = stringBuilder.Length;

				scopeProvider.ForEachScope((scope, state) =>
				{
					var (builder, length) = state;
					builder.Append(MessagePadding);
					builder.Append("=> ");
					builder.Append(_scopeFormatter(scope)).AppendLine();
				}, (stringBuilder, initialLength));

				if (stringBuilder.Length > initialLength)
				{
					stringBuilder.AppendLine();
				}
				return true;
			}
			return false;
		}
	}
}
