using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin.RPC;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

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
				Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

				var configFilePath = Path.Combine(Global.DataDir, "Config.json");
				var config = new Config(configFilePath);
				await config.LoadOrCreateDefaultFileAsync();
				Logger.LogInfo<Config>("Config is successfully initialized.");

				var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
				var roundConfig = new CcjRoundConfig(roundConfigFilePath);
				await roundConfig.LoadOrCreateDefaultFileAsync();
				Logger.LogInfo<CcjRoundConfig>("RoundConfig is successfully initialized.");

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
				Logger.LogCritical<Program>(ex);
			}
		}
	}
}
