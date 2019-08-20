using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	internal class MixerCommand : Command
	{
		public string WalletName { get; set; }
		public bool MixAll { get; set; }
		public bool KeepMixAlive { get; set; }
		public bool ShowHelp { get; set; }
		public Daemon Daemon { get; }

		public MixerCommand(Daemon daemon)
			: base("mix", "Start mixing without the GUI with the specified wallet.")
		{
			Daemon = daemon;
			Options = new OptionSet()
			{
				"usage: mix --wallet:WalletName --mixall --keepalive",
				"",
				"Start mixing without the GUI with the specified wallet.",
				"eg: ./wassabee mix --wallet:MyWalletName --mixall --keepalive --loglevel:info",
				{ "h|help", "Displays help page and exit.", x => ShowHelp = x != null },
				{ "w|wallet=", "The name of the wallet file.", x => WalletName = x },
				{ "mixall", "Mix once even if the coin reached the target anonymity set specified in the config file.", x => MixAll = x != null },
				{ "keepalive", "Do not exit the software after mixing has been finished, rather keep mixing when new money arrives.", x => KeepMixAlive = x != null }
			};
		}

		public override async Task<int> InvokeAsync(IEnumerable<string> args)
		{
			var error = false;
			try
			{
				var extra = Options.Parse(args);
				if (ShowHelp)
				{
					Options.WriteOptionDescriptions(CommandSet.Out);
				}

				if (!error && !ShowHelp)
				{
					await Daemon.RunAsync(WalletName, MixAll, KeepMixAlive);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"commands: There was a problem interpreting the command, please review it.");
				Logger.LogDebug<MixerCommand>(ex);
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return 0;
		}
	}
}
