using Avalonia;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using Mono.Options;
using NBitcoin;
using System;
using System.IO;
using System.Reflection;
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
			var showHelp = false;
			var showVersion = false;
			var verbose = false;
			var options = new OptionSet() {
				{ "v|version", "Displays the Wasabi client version.", x => showVersion = x != null},
				{ "h|help", "Displays this help page and exit.", x => showHelp = x != null},
				{ "vvv|verbose", "Log activity using verbose level.", x => verbose = x != null},
			};
			try
			{
				var extras = options.Parse (args);
				if(extras.Count > 0)
					showHelp = true;
			}
			catch (OptionException) {
				Console.Write ("Option not recognized ");
				ShowHelp(options);
				return;
			}
			if(showHelp)
			{
				ShowHelp(options);
				return;
			}
			if(showVersion)
			{
				Console.WriteLine($"Wasabi version: {Assembly.GetEntryAssembly().GetName().Version}");
				return;
			}

			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

			if(verbose)
			{
				Logger.SetMinimumLevel(LogLevel.Trace);
			}

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

						Global.InitializeNoWallet();
						statusBar = new StatusBarViewModel(Global.Nodes.ConnectedNodes, Global.Synchronizer, Global.UpdateChecker);

						MainWindowViewModel.Instance.StatusBar = statusBar;

						if (Global.Synchronizer.Network != Network.Main)
						{
							MainWindowViewModel.Instance.Title += $" - {Global.Synchronizer.Network}";
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
				MainWindowViewModel.Instance?.Dispose();
				statusBar?.Dispose();
				await Global.DisposeAsync();
				AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
			}
		}

		private static void ShowHelp (OptionSet p)
		{
			Console.WriteLine ("Usage: wassabee [OPTIONS]+");
			Console.WriteLine ("Launches the privacy-oriented bitcoin wallet Wasabi.");
			Console.WriteLine ();
			Console.WriteLine ("Options:");
			p.WriteOptionDescriptions (Console.Out);
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception, "UnobservedTaskException");
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception, "UnhandledException");
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			var result = AppBuilder.Configure<App>();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result
					.UseWin32(true, true)
					.UseSkia();
			}
			else
			{
				result.UsePlatformDetect();
			}

			return result;
		}
	}
}
