﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Daemon;

public class WasabiApplication
{
	public WasabiAppBuilder AppConfig { get; }
	public Global? Global { get; private set; }
	public string ConfigFilePath { get; }
	public Config Config { get; }
	public SingleInstanceChecker SingleInstanceChecker { get; }
	public TerminateService TerminateService { get; }

	public WasabiApplication(WasabiAppBuilder wasabiAppBuilder)
	{
		AppConfig = wasabiAppBuilder;

		ConfigFilePath = Path.Combine(Config.DataDir, "Config.json");
		Directory.CreateDirectory(Config.DataDir);
		Config = new Config(LoadOrCreateConfigs(), wasabiAppBuilder.Arguments);

		SetupLogger();
		Logger.LogDebug($"Wasabi was started with these argument(s): {string.Join(" ", AppConfig.Arguments.DefaultIfEmpty("none"))}.");
		SingleInstanceChecker = new(Config.Network);
		TerminateService = new(TerminateApplicationAsync, AppConfig.Terminate);
	}

	public async Task<ExitCode> RunAsync(Func<Task> afterStarting)
	{
		if (AppConfig.Arguments.Contains("--version"))
		{
			Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
			return ExitCode.Ok;
		}
		if (AppConfig.Arguments.Contains("--help"))
		{
			ShowHelp();
			return ExitCode.Ok;
		}

		if (AppConfig.MustCheckSingleInstance)
		{
			var instanceResult = await SingleInstanceChecker.CheckSingleInstanceAsync();
			if (instanceResult == WasabiInstanceStatus.AnotherInstanceIsRunning)
			{
				Logger.LogDebug("Wasabi is already running, signaled the first instance.");
				return ExitCode.FailedAlreadyRunningSignaled;
			}
			if (instanceResult == WasabiInstanceStatus.Error)
			{
				Logger.LogCritical($"Wasabi is already running, but cannot be signaled");
				return ExitCode.FailedAlreadyRunningError;
			}
		}

		try
		{
			TerminateService.Activate();

			BeforeStarting();

			await afterStarting();
			return ExitCode.Ok;
		}
		finally
		{
			BeforeStopping();
		}
	}

	private void BeforeStarting()
	{
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		Logger.LogSoftwareStarted(AppConfig.AppName);

		Global = CreateGlobal();
	}

	private void BeforeStopping()
	{
		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		// Start termination/disposal of the application.
		TerminateService.Terminate();
		SingleInstanceChecker.Dispose();
		Logger.LogSoftwareStopped(AppConfig.AppName);
	}

	private Global CreateGlobal()
		=> new(Config.DataDir, ConfigFilePath, Config);

	private PersistentConfig LoadOrCreateConfigs()
	{
		PersistentConfig persistentConfig = ConfigManagerNg.LoadFile<PersistentConfig>(ConfigFilePath, createIfMissing: true);

		if (persistentConfig.MigrateOldDefaultBackendUris(out PersistentConfig? newConfig))
		{
			persistentConfig = newConfig;
			ConfigManagerNg.ToFile(ConfigFilePath, persistentConfig);
		}

		return persistentConfig;
	}

	private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		AppConfig.UnobservedTaskExceptionsEventHandler?.Invoke(this, e.Exception);
	}

	private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			AppConfig.UnhandledExceptionEventHandler?.Invoke(this, ex);
		}
	}

	private async Task TerminateApplicationAsync()
	{
		Logger.LogSoftwareStopped(AppConfig.AppName);

		if (Global is { } global)
		{
			await global.DisposeAsync().ConfigureAwait(false);
		}
	}

	private void SetupLogger()
	{
		LogLevel logLevel = Enum.TryParse(Config.LogLevel, ignoreCase: true, out LogLevel parsedLevel)
			? parsedLevel
			: LogLevel.Info;

		Logger.InitializeDefaults(Path.Combine(Config.DataDir, "Logs.txt"), logLevel, Config.LogModes);
	}

	private void ShowHelp()
	{
		Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
		Console.WriteLine($"Usage: {AppConfig.AppName} [OPTION]...");
		Console.WriteLine();
		Console.WriteLine("Available options are:");

		foreach (var (parameter, hint) in Config.GetConfigOptionsMetadata().OrderBy(x => x.ParameterName))
		{
			Console.Write($"  --{parameter.ToLower(),-30} ");
			var hintLines = hint.SplitLines(lineWidth: 40);
			Console.WriteLine(hintLines[0]);
			foreach (var hintLine in hintLines.Skip(1))
			{
				Console.WriteLine($"{' ',-35}{hintLine}");
			}
			Console.WriteLine();
		}
	}
}
