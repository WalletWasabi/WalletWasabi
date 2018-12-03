using Avalonia;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.IO;
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
			var rpc = new RPCClient(new RPCCredentialString { UserPassword = new System.Net.NetworkCredential("bitcoinuser", "Polip69") }, Network.Main);
			WalletWasabi.Models.AllFeeEstimate estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, true, true);
			foreach (var est in estimations.Estimations)
			{
				Console.WriteLine($"{est.Key} {est.Value}");
			}

			Console.ReadKey();
			return;
			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));
			StatusBarViewModel statusBar = null;
			try
			{
				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
				BuildAvaloniaApp()
					.BeforeStarting(async builder =>
					{
						MainWindowViewModel.Instance = new MainWindowViewModel();

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
					}).StartShellApp<AppBuilder, MainWindow>("Wasabi Wallet", null, () => MainWindowViewModel.Instance);
			}
			catch (Exception ex)
			{
				Logger.LogCritical<Program>(ex);
				throw;
			}
			finally
			{
				statusBar?.Dispose();
				await Global.DisposeAsync();

				AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
			}
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception, "UnobservedTaskException");
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception, "UnhandledException");
		}

		private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI();
	}
}
