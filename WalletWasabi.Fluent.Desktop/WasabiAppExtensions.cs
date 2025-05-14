using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;
using ReactiveUI;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Desktop.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Desktop;

public static class WasabiAppExtensions
{
	public static async Task<ExitCode> RunAsGuiAsync(this WasabiApplication app)
	{
		return await app.RunAsync(afterStarting: () => AfterStarting(app));
	}

	private static Task AfterStarting(WasabiApplication app)
	{
		SetupExceptionHandler();

		Logger.LogInfo("Wasabi GUI started.");
		bool runGuiInBackground = app.AppConfig.Arguments.Any(arg => arg.Contains(StartupHelper.SilentArgument));
		UiConfig uiConfig = LoadOrCreateUiConfig(Config.DataDir);
		Services.Initialize(app.Global!, uiConfig, app.SingleInstanceChecker, app.TerminateService);

		using CancellationTokenSource stopLoadingCts = new();

		AppBuilder appBuilder = AppBuilder
			.Configure(() => new App(
				backendInitialiseAsync: async () =>
				{
					// macOS require that Avalonia is started with the UI thread. Hence this call must be delayed to this point.
					await app.Global!.InitializeNoWalletAsync(initializeSleepInhibitor: true, app.TerminateService, stopLoadingCts.Token).ConfigureAwait(false);

					// Make sure that wallet startup set correctly regarding RunOnSystemStartup
					await StartupHelper.ModifyStartupSettingAsync(uiConfig.RunOnSystemStartup).ConfigureAwait(false);
				}, startInBg: runGuiInBackground))
			.UseReactiveUI()
			.SetupAppBuilder()
			.AfterSetup(_ => ThemeHelper.ApplyTheme(uiConfig.DarkModeEnabled ? Theme.Dark : Theme.Light));

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

	private static UiConfig LoadOrCreateUiConfig(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		return UiConfig.LoadFile(Path.Combine(dataDir, "UiConfig.json"));
	}
}
