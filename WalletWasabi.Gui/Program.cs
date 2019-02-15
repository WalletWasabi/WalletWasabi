using Avalonia;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	internal class Program
	{
#pragma warning disable IDE1006 // Naming Styles

		private static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			string loggerFilePath = Path.Combine(Global.DataDir, "Logs.txt");
			Logger.InitializeDefaults(loggerFilePath);
			StatusBarViewModel statusBar = null;
			try
			{
				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				if (args.Any())
				{
					await RunDaemonAsync(args);
					return;
				}

				BuildAvaloniaApp()
					.BeforeStarting(async builder =>
					{
						MainWindowViewModel.Instance = new MainWindowViewModel();

						await InitializeNoUiAsync();

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

		private static async Task InitializeNoUiAsync()
		{
			string configFilePath = Path.Combine(Global.DataDir, "Config.json");
			var config = new Config(configFilePath);
			await config.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo<Config>("Config is successfully initialized.");

			Global.InitializeConfig(config);

			Global.InitializeNoWallet();
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

		private static async Task RunDaemonAsync(string[] args)
		{
			Guard.NotNullOrEmpty(nameof(args), args);
			for (int i = 0; i < args.Length; i++)
			{
				args[i] = Guard.Correct(args[i]);
			}

			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Console, LogMode.File);

			if (!args[0].Equals("daemon", StringComparison.OrdinalIgnoreCase)
				|| args.Length != 2)
			{
				Logger.LogError("Usage: daemon WalletFileName");
				return;
			}

			await InitializeNoUiAsync();

			var walletName = args[1];
			var walletFullPath = Global.GetWalletFullPath(walletName);
			var walletBackupFullPath = Global.GetWalletBackupFullPath(walletName);
			if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
			{
				// The selected wallet is not available any more (someone deleted it?).
				Logger.LogError("The selected wallet doesn't exsist, did you delete it?");
				return;
			}

			KeyManager keyManager = Global.InitializeKeyManager(walletFullPath, walletBackupFullPath);
			await Global.InitializeWalletServiceAsync(keyManager);

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine();
			Logger.LogWarning("Please enter your password:", "Daemon");
			Console.WriteLine();
			Console.WriteLine();
			string password = "";
			do
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				// Backspace Should Not Work
				if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
				{
					password += key.KeyChar;
					Console.Write("*");
				}
				else
				{
					if (key.Key == ConsoleKey.Backspace && password.Length > 0)
					{
						password = password.Substring(0, (password.Length - 1));
						Console.Write("\b \b");
					}
					else if (key.Key == ConsoleKey.Enter)
					{
						break;
					}
				}
			} while (true);

			password = Guard.Correct(password);

			try
			{
				await Global.ChaumianClient.QueueCoinsToMixAsync(password, Global.WalletService.Coins.Where(x => x.Unspent).ToArray());
			}
			catch (SecurityException)
			{
				Logger.LogError("Password is incorrect.", "Daemon");
				return;
			}

			while (Global.ChaumianClient.State.AnyCoinsQueued())
			{
				await Task.Delay(3000);
			}

			await Global.ChaumianClient.DequeueAllCoinsFromMixAsync();
		}
	}
}
