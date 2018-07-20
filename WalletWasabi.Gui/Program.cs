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
						Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

						var configFilePath = Path.Combine(Global.DataDir, "Config.json");
						var config = new Config(configFilePath);
						await config.LoadOrCreateDefaultFileAsync();
						Logger.LogInfo<Config>("Config is successfully initialized.");

						Global.InitializeConfig(config);

						if (!File.Exists(Global.IndexFilePath)) // Load the index file from working folder if we have it.
						{
							var cachedIndexFilePath = Path.Combine("Assets", Path.GetFileName(Global.IndexFilePath));
							if (File.Exists(cachedIndexFilePath))
							{
								File.Copy(cachedIndexFilePath, Global.IndexFilePath, overwrite: false);
							}
						}

						Global.InitializeNoWallet();
						statusBar = new StatusBarViewModel(Global.Nodes.ConnectedNodes, Global.MemPoolService, Global.IndexDownloader, Global.UpdateChecker);

						MainWindowViewModel.Instance.StatusBar = statusBar;

						if (Global.IndexDownloader.Network != Network.Main)
						{
							MainWindowViewModel.Instance.Title += $" - {Global.IndexDownloader.Network}";
						}
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
