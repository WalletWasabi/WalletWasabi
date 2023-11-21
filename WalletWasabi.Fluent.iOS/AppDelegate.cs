using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.iOS;
using Foundation;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.IOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the
// User Interface of the application, as well as listening (and optionally responding) to
// application events from iOS.
[Register("AppDelegate")]
public class AppDelegate : AvaloniaAppDelegate<App>
{
	private WasabiApplication _app;

	private void Program_Main()
	{
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
		        () => App.AfterStarting(_app, AppBuilderIOSExtension.SetupAppBuilder, builder));

	        // App.AfterStarting(_app, AppBuilderAndroidExtension.SetupAppBuilder).RunSynchronously();
        }
        catch (Exception e)
        {
	        Log.Error("WASABI",$"{e}");
        }

        return base.CustomizeAppBuilder(builder);
    }
}