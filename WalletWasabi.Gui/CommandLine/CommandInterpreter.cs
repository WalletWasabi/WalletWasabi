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
		private static Command _mixerCommand = null;
		private static Command _passwordFinderCommand = null;
		private static TextWriter _output = Console.Out;
		private static TextWriter _error = Console.Error;

		// This method is for unit testing porpuse only. 
		public static void Configure(Command mixerCommand, Command passwordFinderCommand, TextWriter output, TextWriter error)
		{
			_mixerCommand = mixerCommand;
			_passwordFinderCommand = passwordFinderCommand;
			_output = output;
			_error = error;
		}

		/// <returns>If the GUI should run or not.</returns>
		public static async Task<bool> ExecuteCommandsAsync(Global global, string[] args)
		{
			var showVersion = false;
			var daemon = new Daemon(global);

			_mixerCommand ??= new MixerCommand(daemon);
			_passwordFinderCommand ??= new PasswordFinderCommand(daemon);
			_output ??= Console.Out;
			_error ??= Console.Error;

/*			if (args.Length == 0)
			{
				return true;
			}
*/
			var suite = new CommandSet("wassabee", _output, _error) 
			{
				"Usage: wassabee [OPTIONS]+",
				"Launches Wasabi Wallet.",
				"",
				{ "v|version", "Displays Wasabi version and exit.",	x => showVersion = x != null },
#if DEBUG
				{ "d|datadir=", "Directory path where store all the Wasabi data.", x => { 
					global.SetDataDir(x); 
					Logger.InitializeDefaults(Path.Combine(global.DataDir, "Logs.txt"));
					} 
				},
#endif
				"",
				"Available commands are:",
				"",
				_mixerCommand,
				_passwordFinderCommand
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
			if (!commandProccessed)
			{
				var nonProcessedOptions = suite.Options.Parse(args);
				// If there is some unprocessed argument then something was wrong.
				if( nonProcessedOptions.Any())
				{
					return false;
				}
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
			_output.WriteLine($"Wasabi Client Version: {Constants.ClientVersion}");
			_output.WriteLine($"Compatible Coordinator Version: {Constants.BackendMajorVersion}");
		}

		private static void ShowHelp(OptionSet p)
		{
			ShowVersion();
			_output.WriteLine();
			_output.WriteLine("Usage: wassabee [OPTIONS]+");
			_output.WriteLine("Launches Wasabi Wallet.");
			_output.WriteLine();
			_output.WriteLine("Options:");
			p.WriteOptionDescriptions(_output);
		}
	}
}
