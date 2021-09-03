using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace WalletWasabi.Backend.Logging
{

	public sealed class WabiSabiLoggerProvider : ILoggerProvider
	{
		private readonly ConcurrentDictionary<string, WabiSabiLogger> _loggers = new();

		public ILogger CreateLogger(string categoryName) =>
			_loggers.GetOrAdd(categoryName, name => new WabiSabiLogger(name));

		public void Dispose()
		{
			_loggers.Clear();
		}
	}
}
