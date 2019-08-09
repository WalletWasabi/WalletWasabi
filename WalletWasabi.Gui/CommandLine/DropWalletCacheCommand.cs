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
	internal class DropWalletCacheCommand : Command
	{
		public string WalletName { get; set; }
		public Network Network { get; set; }
		public Daemon Daemon { get; }
		public bool ShowHelp { get; set; }

		public DropWalletCacheCommand(Daemon daemon)
			: base("dropwalletcache", "Drops the wallet cache for the specified wallet.")
		{
			Daemon = daemon;
			Network = Network.Main;

			Options = new OptionSet()
			{
				"usage: dropwalletcache --wallet:WalletName",
				"",
				"Drops the wallet cache for the specified wallet.",
				"eg: ./wassabee dropwalletcache --wallet:MyWalletName --network:main",
				"",
				{ "w|wallet=", "The name of the wallet file.", x =>  WalletName = x },
				{ "n|network=", "The network for the given file (main, test, reg).", x => Network = GetNetwork(x)},
				{ "h|help", "Show Help.", v => ShowHelp = true}
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
					Logging.Logger.LogCritical<DropWalletCacheCommand>("Missing required argument `--wallet=WalletName`.");
					Logging.Logger.LogCritical<DropWalletCacheCommand>("Use `dropwalletcache --help` for details.");
					error = true;
				}
				else if (Network is null)
				{
					Logging.Logger.LogCritical<DropWalletCacheCommand>("Invalid argument `--network=Network`.");
					Logging.Logger.LogCritical<DropWalletCacheCommand>("Use `dropwalletcache --help` for details.");
					error = true;
				}
				else if (!string.IsNullOrWhiteSpace(WalletName))
				{
					KeyManager km = Daemon.TryGetKeyManagerFromWalletName(WalletName);
					if (km is null)
					{
						error = true;
					}
					else
					{
						string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
						WalletService.DropWalletCache(WalletName, dataDir, Network);
						Logging.Logger.LogInfo("Wallet cache successfully dropped!");
					}
				}
			}
			catch (Exception)
			{
				Logging.Logger.LogCritical<DropWalletCacheCommand>("There was a problem interpreting the command, please review it.");
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
