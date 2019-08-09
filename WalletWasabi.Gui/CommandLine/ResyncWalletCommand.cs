using NBitcoin;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.CommandLine
{
	internal class ResyncWalletCommand : Command
	{
		public string WalletName { get; set; }
		public Network Network { get; set; }
		public Daemon Daemon { get; }
		public bool ShowHelp { get; set; }

		public ResyncWalletCommand(Daemon daemon)
			: base("resyncwallet", "Resyncs the specified wallet to fix wallet balance errors.")
		{
			Daemon = daemon;
			Network = Network.Main;

			Options = new OptionSet()
			{
				"usage: resyncwallet --wallet:WalletName",
				"",
				"Resyncs the specified wallet to fix wallet balance errors.",
				"eg: ./wassabee resyncwallet --wallet:MyWalletName",
				"",
				{ "w|wallet=", "The name of the wallet file.", x =>  WalletName = x },
				{ "n|network=", "The network for the given file (main, test, reg)", x => Network = GetNetwork(x)},
				{ "h|help", "Show Help", v => ShowHelp = true}
			};
		}

		public override Task<int> InvokeAsync(IEnumerable<string> args)
		{
			var error = false;
			try
			{
				var extra = Options.Parse(args);
				if (ShowHelp)
				{
					Options.WriteOptionDescriptions(CommandSet.Out);
				}
				else if (string.IsNullOrWhiteSpace(WalletName))
				{
					Logging.Logger.LogCritical<ResyncWalletCommand>("Missing required argument `--wallet=WalletName`.");
					Logging.Logger.LogCritical<ResyncWalletCommand>("Use `resyncwallet --help` for details.");
					error = true;
				}
				else if (Network is null)
				{
					Logging.Logger.LogCritical<ResyncWalletCommand>("Invalid argument `--network=Network`.");
					Logging.Logger.LogCritical<ResyncWalletCommand>("Use `resyncwallet --help` for details.");
					error = true;
				}
				else if (!string.IsNullOrWhiteSpace(WalletName))
				{
					KeyManager km = Daemon.TryGetKeyManagerFromWalletName(WalletName);
					if (km is null)
					{
						error = true;
					}
					string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
					WalletService.ResyncWallet(WalletName, dataDir, Network);
				}
			}
			catch (Exception)
			{
				Logging.Logger.LogCritical<ResyncWalletCommand>($"There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return Task.FromResult(0);
		}

		private Network GetNetwork(string str)
		{
			if (str == null)
			{
				return Network.Main;
			}

			str = str.ToLower();

			if (str == "main" || str == "mainnet")
			{
				return Network.Main;
			}
			else if (str == "test" || str == "testnet")
			{
				return Network.TestNet;
			}
			else if (str == "reg" || str == "regtest")
			{
				return Network.RegTest;
			}
			else
			{
				return null;
			}
		}
	}
}
