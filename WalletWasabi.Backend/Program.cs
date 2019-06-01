using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend
{
	public class Program
	{
		private static List<string> Last5CoinJoins { get; set; } = new List<string>();
		private static object UpdateUnversionedLock { get; } = new object();

#pragma warning disable IDE1006 // Naming Styles

		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));
				Logger.LogStarting("Wasabi Backend");

				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
				var configFilePath = Path.Combine(Global.DataDir, "Config.json");
				var config = new Config(configFilePath);
				await config.LoadOrCreateDefaultFileAsync();
				Logger.LogInfo<Config>("Config is successfully initialized.");

				var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
				var roundConfig = new CcjRoundConfig(roundConfigFilePath);
				await roundConfig.LoadOrCreateDefaultFileAsync();
				Logger.LogInfo<CcjRoundConfig>("RoundConfig is successfully initialized.");

				var rpc = new RPCClient(
						credentials: RPCCredentialString.Parse(config.BitcoinRpcConnectionString),
						network: config.Network);

				await Global.InitializeAsync(config, roundConfig, rpc);

				try
				{
					Directory.CreateDirectory(UnversionedWebBuilder.UnversionedFolder);
					UnversionedWebBuilder.CreateDownloadTextWithVersionHtml();
					UnversionedWebBuilder.CloneAndUpdateOnionIndexHtml();

					if (File.Exists(Global.Coordinator.CoinJoinsFilePath))
					{
						string[] allLines = File.ReadAllLines(Global.Coordinator.CoinJoinsFilePath);
						Last5CoinJoins = allLines.TakeLast(5).Reverse().ToList();
						UnversionedWebBuilder.UpdateCoinJoinsHtml(Last5CoinJoins);
					}

					Global.Coordinator.CoinJoinBroadcasted += Coordinator_CoinJoinBroadcasted;
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, nameof(Program));
				}

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
			// Note: Don't do finally here. Dispose in Startup.cs.
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception, "UnobservedTaskException");
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception, "UnhandledException");
		}

		private static void Coordinator_CoinJoinBroadcasted(object sender, Transaction tx)
		{
			try
			{
				lock (UpdateUnversionedLock)
				{
					if (Last5CoinJoins.Count > 4)
					{
						Last5CoinJoins.RemoveLast();
					}
					Last5CoinJoins.Insert(0, tx.GetHash().ToString());
					UnversionedWebBuilder.UpdateCoinJoinsHtml(Last5CoinJoins);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Program));
			}
		}
	}
}
