using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	public static class Daemon
	{
		public static async Task<bool> RunAsyncReturnTrueIfContinueWithGuiAsync(string[] args)
		{
			var continueWithGui = true;
			var silent = false;

			var showHelp = false;
			var showVersion = false;
			LogLevel? logLevel = null;
			string walletName = null;
			var doMix = false;

			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Native.AttachParentConsole();
					Console.WriteLine();
				}

				var options = new OptionSet() {
					{ "v|version", "Displays Wasabi version and exit.", x => showVersion = x != null},
					{ "h|help", "Displays help page and exit.", x => showHelp = x != null},
					{ "s|silent", "Do not log to the standard outputs.", x => silent = x != null},
					{ "l|loglevel=", "Sets the level of verbosity for the log TRACE|INFO|WARNING|DEBUG|ERROR.", x => {
						var normalized = x?.ToLower()?.Trim();
						if(normalized == "info") logLevel = LogLevel.Info;
						else if(normalized == "warning")  logLevel = LogLevel.Warning;
						else if(normalized == "error") logLevel = LogLevel.Error;
						else if(normalized == "trace") logLevel = LogLevel.Trace;
						else if(normalized == "debug") logLevel = LogLevel.Debug;
						else {
							Console.WriteLine("ERROR: Log level not recognized.");
							showHelp = true;
						}
					}},
					{ "m|mix", "Start mixing without the GUI with the specified wallet.", x => doMix = x != null},
					{ "w|wallet=", "The specified wallet file.", x => {
						walletName = x?.Trim();
					}}
				};
				try
				{
					var extras = options.Parse(args);
					if (extras.Count > 0)
					{
						showHelp = true;
					}
				}
				catch (OptionException)
				{
					continueWithGui = false;
					Console.WriteLine("Option not recognized.");
					Console.WriteLine();
					ShowHelp(options);
					return continueWithGui;
				}
				if (showHelp)
				{
					continueWithGui = false;
					ShowHelp(options);
					return continueWithGui;
				}
				else if (showVersion)
				{
					continueWithGui = false;
					ShowVersion();
					return continueWithGui;
				}
			}
			finally
			{
				if (silent)
				{
					Native.DettachParentConsole();
				}
			}

			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

			if (logLevel.HasValue)
			{
				Logger.SetMinimumLevel(logLevel.Value);
			}
			if (silent)
			{
				Logger.Modes.Remove(LogMode.Console);
				Logger.Modes.Remove(LogMode.Debug);
			}
			else
			{
				Logger.Modes.Add(LogMode.Console);
				Logger.Modes.Add(LogMode.Debug);
			}

			KeyManager keyManager = null;
			if (walletName != null)
			{
				continueWithGui = false;

				var walletFullPath = Global.GetWalletFullPath(walletName);
				var walletBackupFullPath = Global.GetWalletBackupFullPath(walletName);
				if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
				{
					// The selected wallet is not available any more (someone deleted it?).
					Logger.LogCritical("The selected wallet doesn't exsist, did you delete it?", nameof(Daemon));
					return continueWithGui;
				}

				try
				{
					keyManager = Global.LoadKeyManager(walletFullPath, walletBackupFullPath);
				}
				catch (Exception ex)
				{
					Logger.LogCritical(ex, nameof(Daemon));
					return continueWithGui;
				}
			}

			if (doMix)
			{
				continueWithGui = false;

				if (keyManager is null)
				{
					Logger.LogCritical("Wallet was not supplied. Add --wallet {WalletName}", nameof(Daemon));
					return continueWithGui;
				}

				string password = null;
				var count = 3;
				do
				{
					if (password != null)
					{
						if (count > 0)
						{
							Logger.LogError($"Wrong password. {count} attempts left. Try again.");
						}
						else
						{
							Logger.LogCritical($"Wrong password. {count} attempts left. Exiting...");
							return continueWithGui;
						}
						count--;
					}
					Console.Write("Password: ");

					password = PasswordConsole.ReadPassword();
					password = Guard.Correct(password);
				}
				while (!keyManager.TestPassword(password));

				Logger.LogInfo("Correct password.");

				await Global.InitializeNoUiAsync();
				await Global.InitializeWalletServiceAsync(keyManager);

				await Global.ChaumianClient.QueueCoinsToMixAsync(password, Global.WalletService.Coins.Where(x => !x.Unavailable).ToArray());

				while (Global.ChaumianClient.State.AnyCoinsQueued())
				{
					await Task.Delay(3000);
				}

				await Global.ChaumianClient.DequeueAllCoinsFromMixAsync();
			}

			return continueWithGui;
		}

		private static void ShowVersion()
		{
			Console.WriteLine($"Wasabi Client Version: {Constants.ClientVersion}");
			Console.WriteLine($"Compatible Coordinator Version: {Constants.BackendMajorVersion}");
		}

		private static void ShowHelp(OptionSet p)
		{
			ShowVersion();
			Console.WriteLine();
			Console.WriteLine("Usage: wassabee [OPTIONS]+");
			Console.WriteLine("Launches Wasabi Wallet.");
			Console.WriteLine();
			Console.WriteLine("Options:");
			p.WriteOptionDescriptions(Console.Out);
		}
	}
}
