using Avalonia;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using NBitcoin;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	internal class Program
	{
#pragma warning disable IDE1006 // Naming Styles

		private static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			StatusBarViewModel statusBar = null;
			try
			{
				MainWindowViewModel.Instance = new MainWindowViewModel();
				BuildAvaloniaApp().AfterSetup(async builder =>
				{
					try
					{
						Logger.SetFilePath(Path.Combine(Global.DataDir, "Logs.txt"));
#if RELEASE
						Logger.SetMinimumLevel(LogLevel.Info);
						Logger.SetModes(LogMode.File);
#else
						Logger.SetMinimumLevel(LogLevel.Debug);
						Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
#endif
						var configFilePath = Path.Combine(Global.DataDir, "Config.json");
						var config = new Config(configFilePath);
						await config.LoadOrCreateDefaultFileAsync();
						Logger.LogInfo<Config>("Config is successfully initialized.");

						Global.Initialize(config);
						statusBar = new StatusBarViewModel(Global.Nodes.ConnectedNodes, Global.MemPoolService, Global.IndexDownloader);

						MainWindowViewModel.Instance.StatusBar = statusBar;
					}
					catch (Exception ex)
					{
						Logger.LogCritical<Program>(ex);
					}
				}).StartShellApp<AppBuilder, MainWindow>("Wasabi Wallet", new DefaultLayoutFactory(), () => MainWindowViewModel.Instance);
			}
			catch (Exception ex)
			{
				Logger.LogCritical<Program>(ex);
			}
			finally
			{
				statusBar?.Dispose();
				await Global.DisposeAsync();
			}
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			return AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI();
		}
	}
}
