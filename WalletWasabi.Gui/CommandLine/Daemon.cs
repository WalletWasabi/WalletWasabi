using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	public static class Daemon
	{
		public static void Run(string[] args, out bool continueWithGui)
		{
			var showHelp = false;
			var showVersion = false;
			var verbose = false;
			var options = new OptionSet() {
				{ "v|version", "Displays Wasabi version and exit.", x => showVersion = x != null},
				{ "h|help", "Displays help page and exit.", x => showHelp = x != null},
				{ "vvv|verbose", "Log activity using verbose level.", x => verbose = x != null},
			};
			try
			{
				var extras = options.Parse(args);
				if (extras.Count > 0)
					showHelp = true;
			}
			catch (OptionException)
			{
				Console.WriteLine("Option not recognized.");
				Console.WriteLine();
				ShowHelp(options);

				continueWithGui = false;
				return;
			}
			if (showHelp)
			{
				ShowHelp(options);

				continueWithGui = false;
				return;
			}
			else if (showVersion)
			{
				ShowVersion();

				continueWithGui = false;
				return;
			}

			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

			if (verbose)
			{
				Logger.SetMinimumLevel(LogLevel.Trace);
			}

			continueWithGui = true;
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
