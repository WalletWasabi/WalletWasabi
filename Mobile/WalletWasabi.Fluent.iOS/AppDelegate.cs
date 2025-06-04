using System;
using System.IO;
using Foundation;
using UIKit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.CrashReport;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the
// User Interface of the application, as well as listening (and optionally responding) to
// application events from iOS.
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
	protected override AppBuilder CreateAppBuilder()
	{
		// Crash reporting must be before the "single instance checking".
		Logger.InitializeDefaults(Path.Combine(Config.DataDir, "Logs.txt"), LogLevel.Info);

		// TODO: CrashReporter.TryGetExceptionFromCliArgs
		// TODO: CrashReporterAppBuilder.BuildCrashReporterApp

		var args = Environment.GetCommandLineArgs();

		try
		{
			var androidWalletWasabiAppBuilder = new iOSWalletWasabiAppBuilder();
			WasabiFluentAppBuilder.RunMobileAsync(args, androidWalletWasabiAppBuilder);

			return androidWalletWasabiAppBuilder.AppBuilder;
		}
		catch (Exception ex)
		{
			CrashReporter.Invoke(ex);
			Logger.LogCritical(ex);
			// return 1;
		}

		// TODO: Remove this when the Android app is ready.
		return AppBuilder.Configure<App>().UseiOS();
	}

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
