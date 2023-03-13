using System;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NBitcoin;
using LogLevel = WalletWasabi.Logging.LogLevel;

namespace WalletWasabi.Daemon;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		// Initialize the logger.
		string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
		SetupLogger(dataDir, args);

		var app = WasabiAppBuilder
			.Create("Wasabi Daemon", args)
			.EnsureSingleInstance()
			.OnUnhandledExceptions(LogUnhandledException)
			.OnUnobservedTaskExceptions(LogUnobservedTaskException)
			.Build();

		return await app.RunAsConsoleAsync();
	}

	/// <summary>
	/// Initializes Wasabi Logger. Sets user-defined log-level, if provided.
	/// </summary>
	/// <example>Start Wasabi Wallet with <c>./wassabee --LogLevel=trace</c> to set <see cref="LogLevel.Trace"/>.</example>
	private static void SetupLogger(string dataDir, string[] args)
	{
		LogLevel? logLevel = null;

		foreach (string arg in args)
		{
			if (arg.StartsWith("--LogLevel="))
			{
				string value = arg.Split('=', count: 2)[1];

				if (Enum.TryParse(value, ignoreCase: true, out LogLevel parsedLevel))
				{
					logLevel = parsedLevel;
					break;
				}
			}
		}

		Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"), logLevel);
	}

	private static void LogUnobservedTaskException(object? sender, AggregateException e)
	{
		ReadOnlyCollection<Exception> innerExceptions = e.Flatten().InnerExceptions;

		switch (innerExceptions)
		{
			case [SocketException { SocketErrorCode: SocketError.OperationAborted }]:
			// Source of this exception is NBitcoin library.
			case [OperationCanceledException { Message: "The peer has been disconnected" }]:
				// Until https://github.com/MetacoSA/NBitcoin/pull/1089 is resolved.
				Logger.LogTrace(e);
				break;
			default:
				Logger.LogDebug(e);
				break;
		}
	}

	private static void LogUnhandledException(object? sender, Exception e) =>
		Logger.LogWarning(e);
}
