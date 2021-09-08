using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WalletWasabi.Backend.Logging
{
	public class WabiSabiLoggerProvider : ILoggerProvider, ISupportExternalScope
	{
		private readonly ConcurrentDictionary<string, WabiSabiLogger> _loggers = new();
		private IExternalScopeProvider? _scopeProvider = null;
		private bool _isDisposed = false;
		private readonly Func<object, string> _formatter;

		public WabiSabiLoggerProvider(Func<object, string> formatter)
		{
			_formatter = formatter;
		}

		internal IExternalScopeProvider ScopeProvider =>
			_scopeProvider ??= new LoggerExternalScopeProvider();

		public ILogger CreateLogger(string categoryName) =>
			_loggers.GetOrAdd(categoryName, name => new WabiSabiLogger(this, name, _formatter));

		void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider) =>
			_scopeProvider = scopeProvider;


		protected virtual void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					_loggers.Clear();
				}
			}
		}

		void IDisposable.Dispose()
		{
			try
			{
				Dispose(true);
			}
			catch
			{
			}
			_isDisposed = true;
			GC.SuppressFinalize(this);
		}

		~WabiSabiLoggerProvider()
		{
			if (!_isDisposed)
			{
				Dispose(false);
			}
		}
	}
}
