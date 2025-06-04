using System;
using System.IO;
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.CrashReport;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Android;

[Activity(
	Label = "WalletWasabi.Fluent.Android",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
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
			var androidWalletWasabiAppBuilder = new AndroidWalletWasabiAppBuilder();
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
		return AppBuilder.Configure<App>().UseAndroid();
	}

	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		return base.CustomizeAppBuilder(builder)
			.WithInterFont();
	}
}
