using Mono.Options;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.CommandLine
{
	public static class CommandInterpreter
	{
		public static async Task<bool> ExecuteCommandsAsync(string[] args)
		{
			var showHelp = false;
			var showVersion = false;

			if (args.Length == 0)
			{
				return true;
			}

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
					{ "h|help", "Displays help page and exit.",
						x => showHelp = x != null},
					{ "v|version", "Displays Wasabi version and exit.",
						x => showVersion = x != null},
					"",
					"Available commands are:",
					"",
					new MixerCommand(),
					new PasswordFinderCommand()
				};

				EnsureBackwardCompatibilityWithOldParameters(ref args);
				if (await suite.RunAsync(args) == 0)
				{
					return false;
				}
				if (showHelp)
				{
					ShowHelp(options);
					return false;
				}
				else if (showVersion)
				{
					ShowVersion();
					return false;
				}
			}
			finally
			{
				Native.DettachParentConsole();
			}
			return true;
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
