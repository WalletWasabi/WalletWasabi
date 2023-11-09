using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.iOS;
using Foundation;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.iOS;

public static class Log
{
	public static int Error(string? tag, string msg)
	{
		Console.WriteLine($"[{tag}] {msg}");
		return 0;
	}
}

// The UIApplicationDelegate for the application. This class is responsible for launching the
// User Interface of the application, as well as listening (and optionally responding) to
// application events from iOS.
[Register("AppDelegate")]
public class AppDelegate : AvaloniaAppDelegate<App>
{
	private WasabiApplication _app;
	private UiConfig _uiConfig;

	private void Program_Main()
	{
		_app = WasabiAppBuilder
			.Create("Wasabi GUI", System.Array.Empty<string>())
			.EnsureSingleInstance()
			// .OnUnhandledExceptions(LogUnhandledException)
			// .OnUnobservedTaskExceptions(LogUnobservedTaskException)
			// .OnTermination(TerminateApplication)
			.Build();
	}

	private void Program_RunAsGuiAsync()
	{
		_uiConfig = LoadOrCreateUiConfig(Config.DataDir);
		Services.Initialize(_app.Global!, _uiConfig, _app.SingleInstanceChecker, _app.TerminateService);
	}

	public static UiConfig LoadOrCreateUiConfig(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
		uiConfig.LoadFile(createIfMissing: true);

		return uiConfig;
	}

	public AppDelegate()
	{
		App.LogError = Log.Error;
		Log.Error("WASABI", "MainActivity");
	}

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        builder.WithInterFont();
        Log.Error("WASABI", "CustomizeAppBuilder");
        try
        {
	        Program_Main();

	        // _app.RunAsync(afterStarting: () =>
	        // {
	        //
	        // });
	        _app.Global = _app.CreateGlobal();

	        Program_RunAsGuiAsync();

	        var backendInitialiseAsync = async () =>
	        {
		        using CancellationTokenSource stopLoadingCts = new();

		        // macOS require that Avalonia is started with the UI thread. Hence this call must be delayed to this point.
		        await _app.Global!.InitializeNoWalletAsync(_app.TerminateService, stopLoadingCts.Token).ConfigureAwait(false);

		        // Make sure that wallet startup set correctly regarding RunOnSystemStartup
		        // await StartupHelper.ModifyStartupSettingAsync(uiConfig.RunOnSystemStartup).ConfigureAwait(false);
	        };

	        //var app = (builder.Instance as App);
	        App._backendInitialiseAsync = backendInitialiseAsync;

	        builder.AfterSetup(_ =>
	        {
		        ThemeHelper.ApplyTheme(_uiConfig.DarkModeEnabled ? WalletWasabi.Fluent.Helpers.Theme.Dark : WalletWasabi.Fluent.Helpers.Theme.Light);
	        });
        }
        catch (Exception e)
        {
	        Log.Error("WASABI",$"{e}");
        }

        return base.CustomizeAppBuilder(builder);
    }
}
