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
	public static class CommandInterpreter
	{
		public static void ExecuteCommands(string[] args)
		{
			var showHelp = false;
			var showVersion = false;

			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Native.AttachParentConsole();
					Console.WriteLine();
				}

				OptionSet options = null;
				var suite = new CommandSet("wassabee") {
					"Usage: wassabee [OPTIONS]+",
					"Launches Wasabi Wallet.",
					"",
					{ "h|help", "Displays help page and exit.", x => showHelp = x != null},
					{ "v|version", "Displays Wasabi version and exit.", x => showVersion = x != null},
					"",
					"Available commands are:",
					"",
					new MixerCommand(),
					new PasswordFinderCommand()
				};

				suite.Run(args);
				if (showHelp)
				{
					ShowHelp(options);
					return;
				}
				else if (showVersion)
				{
					ShowVersion();
					return;
				}
			}
			finally
			{
				Native.DettachParentConsole();
			}
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

	class Daemon
	{
		internal static async Task RunAsync(string walletName, LogLevel? logLevel, bool mixAll, bool keepMixAlive, bool silent)
		{
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
			Logger.LogStarting("Wasabi");

			KeyManager keyManager = null;
			if (walletName != null)
			{
				var walletFullPath = Global.GetWalletFullPath(walletName);
				var walletBackupFullPath = Global.GetWalletBackupFullPath(walletName);
				if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
				{
					// The selected wallet is not available any more (someone deleted it?).
					Logger.LogCritical("The selected wallet doesn't exsist, did you delete it?", nameof(Daemon));
					return;
				}

				try
				{
					keyManager = Global.LoadKeyManager(walletFullPath, walletBackupFullPath);
				}
				catch (Exception ex)
				{
					Logger.LogCritical(ex, nameof(Daemon));
					return;
				}
			}

			if (keyManager is null)
			{
				Logger.LogCritical("Wallet was not supplied. Add --wallet {WalletName}", nameof(Daemon));
				return;
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
						return;
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

			await TryQueueCoinsToMixAsync(mixAll, password);

			var mixing = true;
			do
			{
				if (Global.KillRequested) break;
				await Task.Delay(3000);
				if (Global.KillRequested) break;

				bool anyCoinsQueued = Global.ChaumianClient.State.AnyCoinsQueued();

				if (!anyCoinsQueued && keepMixAlive) // If no coins queued and mixing is asked to be kept alive then try to queue coins.
				{
					await TryQueueCoinsToMixAsync(mixAll, password);
				}

				if (Global.KillRequested) break;

				mixing = anyCoinsQueued || keepMixAlive;
			} while (mixing);

			await Global.ChaumianClient.DequeueAllCoinsFromMixAsync();
		}

		private static async Task TryQueueCoinsToMixAsync(bool mixAll, string password)
		{
			try
			{
				if (mixAll)
				{
					await Global.ChaumianClient.QueueCoinsToMixAsync(password, Global.WalletService.Coins.Where(x => !x.Unavailable).ToArray());
				}
				else
				{
					await Global.ChaumianClient.QueueCoinsToMixAsync(password, Global.WalletService.Coins.Where(x => !x.Unavailable && x.AnonymitySet < Global.WalletService.ServiceConfiguration.MixUntilAnonymitySet).ToArray());
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Daemon));
			}
		}
	}

	internal class MixerCommand : Command
	{
		public string LoggingLevel { get; set; }
		public string WalletName { get; set; }
		public bool Silent { get; set; }
		public bool MixAll { get; set; }
		public bool KeepMixAlive { get; set; }
		public bool ShowHelp { get; set; }

		public MixerCommand()
			: base("mix", "Start mixing without the GUI with the specified wallet.")
		{
			Options = new OptionSet() {
				"usage: mix --wallet:wallet-file-path --mixall --keepalive loglevel:log-level",
				"",
				"Start mixing without the GUI with the specified wallet.",
				"eg: mix --wallet:/home/user/.walletwasabi/client/Wallets/MyWallet.json --mixall --keepalive loglevel:info",

				{ "h|help", "Displays help page and exit.", x => ShowHelp = x != null},
				{ "s|silent", "Do not log to the standard outputs.", x => Silent = x != null},
				{ "l|loglevel=", "Sets the level of verbosity for the log TRACE|INFO|WARNING|DEBUG|ERROR.",	x => LoggingLevel = x },
				{ "w|wallet=", "The specified wallet file.", x =>  WalletName = x?.ToLower() },
				{ "mixall", "Mix once even if the coin reached the target anonymity set specified in the config file.", x => MixAll = x != null},
				{ "keepalive", "Don't exit the software after mixing has been finished, rather keep mixing when new money arrives.", x => KeepMixAlive = x != null},
			};
		}

		public override int Invoke(IEnumerable<string> args)
		{
			var error = false;
			LogLevel? logLevel = null;
			try
			{
				var extra = Options.Parse(args);
				if (ShowHelp)
				{
					Options.WriteOptionDescriptions(CommandSet.Out);
				}
				else if (!string.IsNullOrEmpty(LoggingLevel))
				{
 					var normalized = LoggingLevel.Trim();
					if(normalized == "info") logLevel = LogLevel.Info;
					else if(normalized == "warning")  logLevel = LogLevel.Warning;
					else if(normalized == "error") logLevel = LogLevel.Error;
					else if(normalized == "trace") logLevel = LogLevel.Trace;
					else if(normalized == "debug") logLevel = LogLevel.Debug;
					else {
						Console.WriteLine("ERROR: Log level not recognized.");
						Options.WriteOptionDescriptions(CommandSet.Out);
						error = true;
					}
				}

				if(!error && !ShowHelp)
				{
					Daemon.RunAsync(WalletName, logLevel, MixAll, KeepMixAlive, Silent).GetAwaiter().GetResult();
				}
			}
			catch(Exception)
			{
				Console.WriteLine($"commands: There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return 0;
		}
	}
}
