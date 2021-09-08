using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Backend.Logging
{

	public static class WabiSabiLoggerExtensions
	{
		public static ILoggingBuilder AddWabiSabiLogger(
			this ILoggingBuilder builder)
		{
			builder.AddConfiguration();

			builder.Services.AddSingleton<ILoggerProvider,WabiSabiLoggerProvider>(_ =>
				new WabiSabiLoggerProvider(Formatter));

			return builder;
		}

		private static string Formatter<T>(T state) =>
			string.Empty + state switch
			{
				Round round => $"round Id: {round.Id.ToString().Substring(0, 10)} ({round.Phase})",
				_ => state.ToString()
			};
	}
}
