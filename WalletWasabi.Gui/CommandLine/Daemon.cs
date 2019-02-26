using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
			var printConsole = false;
			var showVersion = false;
			LogLevel? logLevel = null;

			try
			{
				if (args.Length > 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Native.AttachParentConsole();
					Console.WriteLine();
				}

				var options = new OptionSet() {
					{ "v|version", "Displays Wasabi version and exit.", x => showVersion = x != null},
					{ "h|help", "Displays help page and exit.", x => showHelp = x != null},
					{ "p|printconsole", "Log to the standard output", x => printConsole = x != null},
					{ "l|loglevel=", "Sets the level of verbosity for the log TRACE|INFO|WARN|DEBUG|ERROR.", x => {
						var normalized = x.ToLower().Trim();
						if(normalized == "info") logLevel = LogLevel.Info;
						else if(normalized == "warn")  logLevel = LogLevel.Warning;
						else if(normalized == "error") logLevel = LogLevel.Error;
						else if(normalized == "trace") logLevel = LogLevel.Trace;
						else if(normalized == "debug") logLevel = LogLevel.Debug;
						else {
							Console.WriteLine("ERROR: Log level not recognized.");
							showHelp = true;
						}
					}},
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
			}
			finally
			{
				if (!printConsole)
					Native.DettachParentConsole();
			}

			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

			if (logLevel.HasValue)
			{
				Logger.SetMinimumLevel(logLevel.Value);
			}
			if (printConsole && !Logger.Modes.Contains(LogMode.Console))
			{
				Logger.Modes.Add(LogMode.Console);
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
