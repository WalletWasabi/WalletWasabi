using Mono.Options;
using System;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;

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
}
