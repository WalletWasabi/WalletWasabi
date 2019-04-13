using Mono.Options;
using System;
using System.Collections.Generic;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
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

				{ "h|help", "Displays help page and exit.",
					x => ShowHelp = x != null},
				{ "s|silent", "Do not log to the standard outputs.",
					x => Silent = x != null},
				{ "l|loglevel=", "Sets the level of verbosity for the log TRACE|INFO|WARNING|DEBUG|ERROR.",
					x => LoggingLevel = x },
				{ "w|wallet=", "The specified wallet file.",
					x =>  WalletName = x?.ToLower() },
				{ "mixall", "Mix once even if the coin reached the target anonymity set specified in the config file.",
					x => MixAll = x != null},
				{ "keepalive", "Don't exit the software after mixing has been finished, rather keep mixing when new money arrives.",
					x => KeepMixAlive = x != null},
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
					if (normalized == "info")
					{
						logLevel = LogLevel.Info;
					}
					else if (normalized == "warning")
					{
						logLevel = LogLevel.Warning;
					}
					else if (normalized == "error")
					{
						logLevel = LogLevel.Error;
					}
					else if (normalized == "trace")
					{
						logLevel = LogLevel.Trace;
					}
					else if (normalized == "debug")
					{
						logLevel = LogLevel.Debug;
					}
					else
					{
						Console.WriteLine("ERROR: Log level not recognized.");
						Options.WriteOptionDescriptions(CommandSet.Out);
						error = true;
					}
				}

				if (!error && !ShowHelp)
				{
					Daemon.RunAsync(WalletName, logLevel, MixAll, KeepMixAlive, Silent).GetAwaiter().GetResult();
				}
			}
			catch (Exception)
			{
				Console.WriteLine($"commands: There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return 0;
		}
	}
}
