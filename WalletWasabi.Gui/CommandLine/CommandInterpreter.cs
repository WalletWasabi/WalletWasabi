using Mono.Options;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	public class CommandInterpreter
	{
		/// <returns>If the GUI should run or not.</returns>
		public async Task<bool> ExecuteCommandsAsync(string[] args, MixerCommand mixerCommand, PasswordFinderCommand passwordFinderCommand)
		{
			var showHelp = false;
			var showVersion = false;

			if (args.Length == 0)
			{
				return true;
			}

			var suite = new CommandSet("wassabee")
			{
				"Usage: wassabee [OPTIONS]+",
				"Launches Wasabi Wallet.",
				"",
				{ "h|help", "Displays help page and exit.", x => showHelp = x != null },
				{ "v|version", "Displays Wasabi version and exit.", x => showVersion = x != null },
				"",
				"Available commands are:",
				"",
				mixerCommand,
				passwordFinderCommand
			};

			EnsureBackwardCompatibilityWithOldParameters(ref args);
			if (await suite.RunAsync(args) == 0)
			{
				return false;
			}
			if (showHelp)
			{
				ShowHelp(suite.Options);
				return false;
			}
			else if (showVersion)
			{
				ShowVersion();
				return false;
			}

			return false;
		}

		private void EnsureBackwardCompatibilityWithOldParameters(ref string[] args)
		{
			var listArgs = args.ToList();
			if (listArgs.Remove("--mix") || listArgs.Remove("-m"))
			{
				listArgs.Insert(0, "mix");
			}
			args = listArgs.ToArray();
		}

		private void ShowVersion()
		{
			Console.WriteLine($"Wasabi Client Version: {Constants.ClientVersion}");
			Console.WriteLine($"Compatible Coordinator Version: {Constants.ClientSupportBackendVersionText}");
			Console.WriteLine($"Compatible Bitcoin Core and Bitcoin Knots Versions: {Constants.BitcoinCoreVersion}");
			Console.WriteLine($"Compatible Hardware Wallet Interface Version: {Constants.HwiVersion}");
		}

		private void ShowHelp(OptionSet p)
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
