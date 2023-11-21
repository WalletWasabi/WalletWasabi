using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Android.App;
using Android.Content.PM;
using Android.Util;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;
using Config = WalletWasabi.Daemon.Config;

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

	private void Program_Main()
	{
		Global.IsTorEnabled = false;

		_app = WasabiAppBuilder
			.Create("Wasabi GUI", System.Array.Empty<string>())
			// TODO:
			// .EnsureSingleInstance()
			// TODO:
			// .OnUnhandledExceptions(LogUnhandledException)
			// TODO:
			// .OnUnobservedTaskExceptions(LogUnobservedTaskException)
			// TODO:
			// .OnTermination(TerminateApplication)
			.Build();
	}

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Log.Error("WASABI", "CustomizeAppBuilder");

        try
        {
	        Program_Main();

	        // WasabiAppExtensions.RunAsDesktopGuiAsync
	        // await _app.RunAsync(afterStarting: () => App.AfterStarting(_app, AppBuilderAndroidExtension.SetupAppBuilder));
	        // _app.RunAsync(afterStarting: () => App.AfterStarting(_app, AppBuilderAndroidExtension.SetupAppBuilder)).RunSynchronously();

	        // TODO: Pass builder to AfterStarting
	        _app.RunAsyncMobile(afterStarting:
		        () => App.AfterStarting(_app, AppBuilderAndroidExtension.SetupAppBuilder, builder));

	       // App.AfterStarting(_app, AppBuilderAndroidExtension.SetupAppBuilder).RunSynchronously();
        }
        catch (Exception e)
        {
	        Log.Error("WASABI", $"{e}");
        }

        return base.CustomizeAppBuilder(builder);
    }
}
