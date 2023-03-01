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
	private static Global? Global;

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static async Task<int> Main(string[] args)
	{
		// Initialize the logger.
		string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
		SetupLogger(dataDir, args);

		Config config = LoadOrCreateConfigs(dataDir);

		Logger.LogDebug($"Wasabi Daemon was started with these argument(s): {(args.Any() ? string.Join(" ", args) : "none")}.");

		var mustExit = await CheckSingleInstanceAsync(config.Network);

		// Now run the GUI application.
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		Exception? exceptionToReport = null;
		TerminateService terminateService = new(TerminateApplicationAsync, () => { });

		try
		{
			Global = CreateGlobal(dataDir, config);
			await Global.InitializeNoWalletAsync(terminateService);
			while (true)
			{
				Console.Read();
			}
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogDebug(ex);
		}
		catch (Exception ex)
		{
			exceptionToReport = ex;
			Logger.LogCritical(ex);
		}

		// Start termination/disposal of the application.
		terminateService.Terminate();

		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		Logger.LogSoftwareStopped("Wasabi");

		return exceptionToReport is { } ? 1 : 0;
	}

	private static async Task<bool> CheckSingleInstanceAsync(Network network)
	{
		// Start single instance checker that is active over the lifetime of the application.
		await using SingleInstanceChecker singleInstanceChecker = new(network);

		try
		{
			await singleInstanceChecker.EnsureSingleOrThrowAsync();
		}
		catch (OperationCanceledException)
		{
			// We have successfully signalled the other instance and that instance should pop up
			// so user will think he has just run the application.
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogCritical(ex);
			return true;
		}

		return false;
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

	private static Config LoadOrCreateConfigs(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		Config config = new(Path.Combine(dataDir, "Config.json"));
		config.LoadFile(createIfMissing: true);

		return config;
	}

	private static Global CreateGlobal(string dataDir, Config config)
	{
		var walletManager = new WalletManager(config.Network, dataDir, new WalletDirectories(config.Network, dataDir));

		return new Global(dataDir, config, walletManager);
	}

	/// <summary>
	/// Do not call this method it should only be called by TerminateService.
	/// </summary>
	private static async Task TerminateApplicationAsync()
	{
		Logger.LogSoftwareStopped("Wasabi GUI");

		if (Global is { } global)
		{
			await global.DisposeAsync().ConfigureAwait(false);
		}
	}


	private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		ReadOnlyCollection<Exception> innerExceptions = e.Exception.Flatten().InnerExceptions;

		switch (innerExceptions)
		{
			case [SocketException { SocketErrorCode: SocketError.OperationAborted }]:
			// Source of this exception is NBitcoin library.
			case [OperationCanceledException { Message: "The peer has been disconnected" }]:
				// Until https://github.com/MetacoSA/NBitcoin/pull/1089 is resolved.
				Logger.LogTrace(e.Exception);
				break;
			default:
				Logger.LogDebug(e.Exception);
				break;
		}
	}

	private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}
}
