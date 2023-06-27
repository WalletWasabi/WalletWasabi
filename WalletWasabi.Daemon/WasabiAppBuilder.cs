using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;

namespace WalletWasabi.Daemon;

public enum ExitCode
{
	Ok,
	FailedAlreadyRunningSignaled,
	FailedAlreadyRunningError,
}

public class WasabiApplication
{
	public WasabiAppBuilder AppConfig { get; }
	public Global? Global { get; private set; }
	public Config Config { get; }
	public SingleInstanceChecker SingleInstanceChecker { get; }
	public TerminateService TerminateService { get; }

	public WasabiApplication(WasabiAppBuilder wasabiAppBuilder)
	{
		AppConfig = wasabiAppBuilder;
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

		Logger.LogSoftwareStarted($"{AppConfig.AppName} was started.");

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
		=> new(Config.DataDir, Config);

	private PersistentConfig LoadOrCreateConfigs()
	{
		Directory.CreateDirectory(Config.DataDir);

		PersistentConfig persistentConfig = new(Path.Combine(Config.DataDir, "Config.json"));
		persistentConfig.LoadFile(createIfMissing: true);

		if (persistentConfig.MigrateOldDefaultBackendUris())
		{
			// Logger.LogInfo("Configuration file with the new coordinator API URIs was saved.");
			persistentConfig.ToFile();
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
		Logger.InitializeDefaults(Path.Combine(Config.DataDir, "Logs.txt"), logLevel);
	}
}

public record WasabiAppBuilder(string AppName, string[] Arguments)
{
	internal bool MustCheckSingleInstance { get; init; }
	internal EventHandler<Exception>? UnhandledExceptionEventHandler { get; init; }
	internal EventHandler<AggregateException>? UnobservedTaskExceptionsEventHandler { get; init; }
	internal Action Terminate { get; init; } = () => { };

	public WasabiAppBuilder EnsureSingleInstance(bool ensure = true) =>
		this with { MustCheckSingleInstance = ensure };

	public WasabiAppBuilder OnUnhandledExceptions(EventHandler<Exception> handler) =>
		this with { UnhandledExceptionEventHandler = handler };

	public WasabiAppBuilder OnUnobservedTaskExceptions(EventHandler<AggregateException> handler) =>
		this with { UnobservedTaskExceptionsEventHandler = handler };

	public WasabiAppBuilder OnTermination(Action action) =>
		this with { Terminate = action };
	public WasabiApplication Build() =>
		new(this);

	public static WasabiAppBuilder Create(string appName, string[] args) =>
		new(appName, args);
}

public static class WasabiAppExtensions
{
	public static async Task<ExitCode> RunAsConsoleAsync(this WasabiApplication app)
	{
		void ProcessCommands()
		{
			var arguments = app.AppConfig.Arguments;
			var walletNames = ArgumentHelpers
				.GetValues("wallet", arguments, x => x)
				.Distinct();

			foreach (var walletName in walletNames)
			{
				try
				{
					var wallet = app.Global!.WalletManager.GetWalletByName(walletName);
					app.Global!.WalletManager.StartWalletAsync(wallet).ConfigureAwait(false);
				}
				catch (InvalidOperationException)
				{
					Logger.LogWarning($"Wallet '{walletName}' was not found. Ignoring...");
				}
			}
		}

		return await app.RunAsync(
			async () =>
			{
				await app.Global!.InitializeNoWalletAsync(app.TerminateService).ConfigureAwait(false);

				ProcessCommands();

				await app.TerminateService.TerminationRequested.Task.ConfigureAwait(false);
			}).ConfigureAwait(false);
	}
}
