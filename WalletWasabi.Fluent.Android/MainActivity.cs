using System.IO;
using System.Threading;
using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Android;

[Activity(
    Label = "Wasabi Wallet",
    Theme = "@style/MyTheme.Main",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity<App>
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
		Services.Initialize(_app.Global!, _uiConfig, _app.SingleInstanceChecker);
	}

	public static UiConfig LoadOrCreateUiConfig(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
		uiConfig.LoadFile(createIfMissing: true);

		return uiConfig;
	}

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        builder.WithInterFont();

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

        return base.CustomizeAppBuilder(builder);
    }
}
