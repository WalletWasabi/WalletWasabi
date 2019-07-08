using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend
{
	public class InitConfigStartupTask : IStartupTask
	{
		private readonly Global _global;
		private readonly HostingEnvironment _hostingEnvironment;
		private UnversionedWebBuilder _unversionedWebBuilder;

		private static List<string> Last5CoinJoins { get; set; } = new List<string>();
		private static object UpdateUnversionedLock { get; } = new object();

		public InitConfigStartupTask(Global global, IHostingEnvironment hostingEnvironment)
		{
			_global = global;
			_hostingEnvironment = (HostingEnvironment)hostingEnvironment;
		}

		public async Task ExecuteAsync(CancellationToken cancellationToken)
		{
			Logger.InitializeDefaults(Path.Combine(_global.DataDir, "Logs.txt"));
			Logger.LogStarting("Wasabi Backend");

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
			var configFilePath = Path.Combine(_global.DataDir, "Config.json");
			var config = new Config(configFilePath);
			await config.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo<Config>("Config is successfully initialized.");

			var roundConfigFilePath = Path.Combine(_global.DataDir, "CcjRoundConfig.json");
			var roundConfig = new CcjRoundConfig(roundConfigFilePath);
			await roundConfig.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo<CcjRoundConfig>("RoundConfig is successfully initialized.");

			var rpc = new RPCClient(
					credentials: RPCCredentialString.Parse(config.BitcoinRpcConnectionString),
					network: config.Network);

			await _global.InitializeAsync(config, roundConfig, rpc);

			try
			{
				_unversionedWebBuilder = new UnversionedWebBuilder(_hostingEnvironment.WebRootPath);
				Directory.CreateDirectory(_unversionedWebBuilder.UnversionedFolder);
				_unversionedWebBuilder.CreateDownloadTextWithVersionHtml();
				_unversionedWebBuilder.CloneAndUpdateOnionIndexHtml();

				if (File.Exists(_global.Coordinator.CoinJoinsFilePath))
				{
					string[] allLines = File.ReadAllLines(_global.Coordinator.CoinJoinsFilePath);
					Last5CoinJoins = allLines.TakeLast(5).Reverse().ToList();
					_unversionedWebBuilder.UpdateCoinJoinsHtml(_global, Last5CoinJoins);
				}

				_global.Coordinator.CoinJoinBroadcasted += Coordinator_CoinJoinBroadcasted;
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
					_unversionedWebBuilder.UpdateCoinJoinsHtml(_global, Last5CoinJoins);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Program));
			}
		}
	}
}
