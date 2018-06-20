using Avalonia;
using Avalonia.Threading;
using System;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell;
using WalletWasabi.Logging;
using System.IO;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Logger.SetFilePath(Path.Combine(Global.DataDir, "Logs.txt"));
#if RELEASE
				Logger.SetMinimumLevel(LogLevel.Info);
				Logger.SetModes(LogMode.File);
#else
			Logger.SetMinimumLevel(LogLevel.Debug);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
#endif

			BuildAvaloniaApp().AfterSetup(async builder =>
			{
				var configFilePath = Path.Combine(Global.DataDir, "Config.json");
				var config = new Config(configFilePath);
				await config.LoadOrCreateDefaultFileAsync();
				Logger.LogInfo<Config>("Config is successfully initialized.");

				await Global.InitializeAsync(config);
			}).StartShellApp("Wasabi Wallet");
		}

		private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI();
	}
}
