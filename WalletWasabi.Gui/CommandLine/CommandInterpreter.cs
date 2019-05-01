using Mono.Options;
using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	public static class CommandInterpreter
	{
		/// <returns>If the GUI should run or not.</returns>
		public static async Task<bool> ExecuteCommandsAsync(string[] args)
		{
			var showVersion = false;
			var pass = false;
			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

			var suite = new CommandSet("wassabee") {
					"Usage: wassabee [OPTIONS]+",
					"Launches Wasabi Wallet.",
					"",
					{ "v|version", "Displays Wasabi version and exit.",
						x => showVersion = x != null},
					{ "d|datadir=", "Directory path where store all the Wasabi data.",
						x => { Global.SetDataDir(x); pass = true; }},
					"",
					"Available commands are:",
					"",
					new MixerCommand(),
					new PasswordFinderCommand()
				};

			EnsureBackwardCompatibilityWithOldParameters(ref args);
			var commandProccessed = await suite.RunAsync(args) == 0;

			if (suite.ShowHelp)
			{
				return false; // do not run GUI
			}
			else if (showVersion)
			{
				ShowVersion();
				return false; // do not run GUI
			}

			// if no command was provide we have to lunch the GUI
			if (!commandProccessed && pass)
			{
				return true; // run GUI
			}

			return false;
		}

		private static void EnsureBackwardCompatibilityWithOldParameters(ref string[] args)
		{
			var listArgs = args.ToList();
			if (listArgs.Remove("--mix") || listArgs.Remove("-m"))
			{
				listArgs.Insert(0, "mix");
			}
			args = listArgs.ToArray();
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
