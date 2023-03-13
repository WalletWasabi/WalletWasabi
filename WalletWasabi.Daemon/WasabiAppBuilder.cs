using System;
using System.IO;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;

namespace WalletWasabi.Daemon;

public class WApp
{
	public WasabiAppBuilder AppConfig { get; }
	public Global? Global { get; private set; }
	public Settings Settings { get; }
	public SingleInstanceChecker SingleInstanceChecker { get; }

	public string DataDir { get; } = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));

	public WApp(WasabiAppBuilder wasabiAppBuilder)
	{
		AppConfig = wasabiAppBuilder;
		Settings = new Settings(LoadOrCreateConfigs(), wasabiAppBuilder.Arguments);
		SingleInstanceChecker = new(Settings.Network);
	}

	public async Task<int> RunAsync(Action afterStarting)
	{
		if (AppConfig.MustCheckSingleInstance)
		{
			var instanceResult = await SingleInstanceChecker.CheckSingleInstanceAsync();
			if (instanceResult == WasabiInstanceStatus.AnotherInstanceIsRunning)
			{
				Logger.LogDebug("Wasabi is already running, signaled the first instance.");
				return 1;
			}
			if (instanceResult == WasabiInstanceStatus.PortIsBeingUser)
			{
				Logger.LogCritical($"Wasabi is already running, but cannot be signaled");
				return 1;
			}
		}

		TerminateService terminateService = new(TerminateApplicationAsync, AppConfig.Terminate);

		try
		{
			await BeforeStartingAsync(terminateService);

			afterStarting();
			return 0;
		}
		finally
		{
			BeforeStopping(terminateService);
		}
	}

	private async Task BeforeStartingAsync(TerminateService terminateService)
	{
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		Logger.LogSoftwareStarted($"{AppConfig.AppName} was started.");

		Global = CreateGlobal();
		await Global.InitializeNoWalletAsync(terminateService);
	}

	private void BeforeStopping(TerminateService terminateService)
	{
		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		// Start termination/disposal of the application.
		terminateService.Terminate();
		SingleInstanceChecker.Dispose();
		Logger.LogSoftwareStopped(AppConfig.AppName);
	}

	private Global CreateGlobal()
	{
		var walletManager = new WalletManager(Settings.Network, DataDir, new WalletDirectories(Settings.Network, DataDir));
		return new Global(DataDir, Settings, walletManager);
	}

	private Config LoadOrCreateConfigs()
	{
		Directory.CreateDirectory(DataDir);

		Config config = new(Path.Combine(DataDir, "Config.json"));
		config.LoadFile(createIfMissing: true);

		if (config.MigrateOldDefaultBackendUris())
		{
			// Logger.LogInfo("Configuration file with the new coordinator API URIs was saved.");
			config.ToFile();
		}

		return config;
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

}

public record WasabiAppBuilder(string AppName, string[] Arguments)
{
	internal bool MustCheckSingleInstance { get; init; }
	internal EventHandler<Exception>? UnhandledExceptionEventHandler { get; init; }
	internal EventHandler<AggregateException>? UnobservedTaskExceptionsEventHandler { get; init; }
	internal Action Terminate { get; init; } = () => { };

	public WasabiAppBuilder ApplicationName(string appName) =>
		this with { AppName = appName };

	public WasabiAppBuilder EnsureSingleInstance(bool ensure = true) =>
		this with { MustCheckSingleInstance = ensure };

	public WasabiAppBuilder OnUnhandledExceptions(EventHandler<Exception> handler) =>
		this with { UnhandledExceptionEventHandler = handler };

	public WasabiAppBuilder OnUnobservedTaskExceptions(EventHandler<AggregateException> handler) =>
		this with { UnobservedTaskExceptionsEventHandler = handler };

	public WasabiAppBuilder OnTermination(Action action) =>
		this with { Terminate = action };
	public WApp Build() =>
		new(this);

	public static WasabiAppBuilder Create(string appName, string[] args) =>
		new(appName, args);
}

public static class WasabiAppExtensions
{
	public static async Task<int> RunAsConsoleAsync(this WApp app)
	{
		return await app.RunAsync(
			() =>
			{
				while (true)
				{
					Console.Read();
				}
			});
	}
}
