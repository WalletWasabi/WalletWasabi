using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend
{
	public class InitConfigStartupTask : IStartupTask
	{
		private static List<string> Last5CoinJoins { get; set; } = new List<string>();
		private static object UpdateUnversionedLock { get; } = new object();
		public UnversionedWebBuilder UnversionedWebBuilder { get; }
		public Global Global { get; }

		public InitConfigStartupTask(Global global, IHostingEnvironment hostingEnvironment)
		{
			Global = global;
			UnversionedWebBuilder = new UnversionedWebBuilder(global, ((HostingEnvironment)hostingEnvironment).WebRootPath);
		}

		public async Task ExecuteAsync(CancellationToken cancellationToken)
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

			string host = config.GetBitcoinCoreRpcEndPoint().ToString(config.Network.RPCPort);
			var rpc = new RPCClient(
					authenticationString: config.BitcoinRpcConnectionString,
					hostOrUri: host,
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
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception, "UnobservedTaskException");
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception, "UnhandledException");
		}

		private void Coordinator_CoinJoinBroadcasted(object sender, Transaction tx)
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
