using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NBitcoin.RPC;
using System.Net;
using WalletWasabi.ChaumianCoinJoin;

namespace WalletWasabi.Backend
{
    public class Program
    {
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				Logger.SetFilePath(Path.Combine(Global.DataDir, "Logs.txt"));
				Logger.SetMinimumLevel(LogLevel.Info);
				Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);

				var configFilePath = Path.Combine(Global.DataDir, "Config.json");
				var config = new Config(configFilePath);
				await config.LoadOrCreateDefaultFileAsync();

				var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
				var roundConfig = new CcjRoundConfig(roundConfigFilePath);
				await roundConfig.LoadOrCreateDefaultFileAsync();

				var rpc = new RPCClient(
						credentials: new RPCCredentialString
						{
							UserPassword = new NetworkCredential(config.BitcoinRpcUser, config.BitcoinRpcPassword)
						},
						network: config.Network);

				await Global.InitializeAsync(config, roundConfig, rpc);

				var endPoint = "http://localhost:37127/";

				using (var host = WebHost.CreateDefaultBuilder(args)
					.UseStartup<Startup>()
					.UseUrls(endPoint)
					.Build())
				{
					await host.RunAsync();
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<Program>(ex);
			}
		}
    }
}
