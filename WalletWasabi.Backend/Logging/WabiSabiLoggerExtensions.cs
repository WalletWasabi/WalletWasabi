using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace WalletWasabi.Backend.Logging
{

	public static class WabiSabiLoggerExtensions
	{
		public static ILoggingBuilder AddWabiSabiLogger(
			this ILoggingBuilder builder)
		{
			builder.AddConfiguration();

			builder.Services.AddSingleton<ILoggerProvider, WabiSabiLoggerProvider>();

			return builder;
		}
	}
}
