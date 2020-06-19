using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	public class MixerCommand : Command
	{
		public MixerCommand(Daemon daemon)
			: base("mix", "Start mixing without the GUI with the specified wallet.")
		{
			Daemon = daemon;
			Options = new OptionSet()
			{
				"usage: mix --wallet:WalletName --keepalive",
				"",
				"Start mixing without the GUI with the specified wallet.",
				"eg: ./wassabee mix --wallet:MyWalletName --keepalive",
				{ "h|help", "Displays help page and exit.", x => ShowHelp = x != null },
				{ "w|wallet=", "The name of the wallet file.", x => WalletName = x },
				{ "destination=", "The name of the destination wallet file.", x => DestinationWalletName = x },
				{ "keepalive", "Do not exit the software after mixing has been finished, rather keep mixing when new money arrives.", x => KeepMixAlive = x != null }
			};
		}

		public string WalletName { get; set; }
		public string DestinationWalletName { get; set; }
		public bool KeepMixAlive { get; set; }
		public bool ShowHelp { get; set; }
		public Daemon Daemon { get; }

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
					await Daemon.RunAsync(WalletName, DestinationWalletName ?? WalletName, KeepMixAlive);
				}
			}
			catch (Exception ex)
			{
				if (!(ex is OperationCanceledException))
				{
					Logger.LogCritical(ex);
				}

				Console.WriteLine($"commands: There was a problem interpreting the command, please review it.");
				Logger.LogDebug(ex);
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return 0;
		}
	}
}
