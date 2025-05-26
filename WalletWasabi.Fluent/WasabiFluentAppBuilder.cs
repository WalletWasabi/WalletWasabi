using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent;

public static class WasabiFluentAppBuilder
{
	public static async Task<int> RunAsync(string[] args, IWalletWasabiAppBuilder walletWasabiAppBuilder)
	{
		var app = WasabiAppBuilder
			.Create("Wasabi GUI", args)
			.EnsureSingleInstance()
			.OnUnhandledExceptions(LogUnhandledException)
			.OnUnobservedTaskExceptions(LogUnobservedTaskException)
			.OnTermination(TerminateApplication)
			.Build();

		var exitCode = await app.RunAsync(afterStarting: () => AfterStarting(app, walletWasabiAppBuilder));

		if (app.TerminateService.GracefulCrashException is not null)
		{
			throw app.TerminateService.GracefulCrashException;
		}

		TryInstallNewVersion(app, exitCode);

		return (int)exitCode;
	}

	private static Task AfterStarting(
		WasabiApplication app,
		IWalletWasabiAppBuilder walletWasabiAppBuilder)
	{
		SetupExceptionHandler();

		Logger.LogInfo("Wasabi GUI started.");
		bool runGuiInBackground = app.AppConfig.Arguments.Any(arg => arg.Contains(StartupHelper.SilentArgument));
		var uiConfig = InitializeDependencies(app);

		using CancellationTokenSource stopLoadingCts = new();

		var appBuilder = BuildDesktopAppBuilder(app, stopLoadingCts, uiConfig, runGuiInBackground, walletWasabiAppBuilder);

		if (app.TerminateService.CancellationToken.IsCancellationRequested)
		{
			Logger.LogDebug("Skip starting Avalonia UI as requested the application to stop.");
			stopLoadingCts.Cancel();
		}
		else
		{
			appBuilder.StartWithClassicDesktopLifetime(app.AppConfig.Arguments);
		}

		return Task.CompletedTask;
	}

	private static AppBuilder BuildDesktopAppBuilder(
		WasabiApplication app,
		CancellationTokenSource stopLoadingCts,
		UiConfig uiConfig,
		bool runGuiInBackground,
		IWalletWasabiAppBuilder walletWasabiAppBuilder)
	{
		var appBuilder = AppBuilder
			.Configure(() => new App(
				backendInitialiseAsync: async () => await BackendInitialiseAsync(app, stopLoadingCts, uiConfig),
				startInBg: runGuiInBackground));

		return walletWasabiAppBuilder
			.SetupAppBuilder(appBuilder)
			.AfterSetup(_ => ThemeHelper.ApplyTheme(uiConfig.DarkModeEnabled ? Theme.Dark : Theme.Light));
	}

	private static async Task BackendInitialiseAsync(WasabiApplication app, CancellationTokenSource stopLoadingCts, UiConfig uiConfig)
	{
		// macOS require that Avalonia is started with the UI thread. Hence this call must be delayed to this point.
		await app.Global!.InitializeNoWalletAsync(initializeSleepInhibitor: true, app.TerminateService, stopLoadingCts.Token).ConfigureAwait(false);

		// Make sure that wallet startup set correctly regarding RunOnSystemStartup
		await StartupHelper.ModifyStartupSettingAsync(uiConfig.RunOnSystemStartup).ConfigureAwait(false);
	}

	private static void TryInstallNewVersion(WasabiApplication app, ExitCode exitCode)
	{
		if (exitCode == ExitCode.Ok && app.Global is {Status: {InstallOnClose: true, InstallerFilePath: var installerFilePath}})
		{
			Installer.StartInstallingNewVersion(installerFilePath);
		}
	}

	private static void SetupExceptionHandler()
	{
		RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
		{
			if (Debugger.IsAttached)
			{
				Debugger.Break();
			}

			Logger.LogError(ex);

			RxApp.MainThreadScheduler.Schedule(() => throw new ApplicationException("Exception has been thrown in unobserved ThrownExceptions", ex));
		});
	}

	private static UiConfig InitializeDependencies(WasabiApplication app)
	{
		UiConfig uiConfig = LoadOrCreateUiConfig(Config.DataDir);
		Services.Initialize(app.Global!, uiConfig, app.SingleInstanceChecker, app.TerminateService);
		return uiConfig;
	}

	private static UiConfig LoadOrCreateUiConfig(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		return UiConfig.LoadFile(Path.Combine(dataDir, "UiConfig.json"));
	}

	/// <summary>
	/// Do not call this method it should only be called by TerminateService.
	/// </summary>
	private static void TerminateApplication()
	{
		Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Close());
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
