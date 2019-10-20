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
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	public class InitConfigStartupTask : IStartupTask
	{
		public WebsiteTorifier WebsiteTorifier { get; }
		public Global Global { get; }

		public InitConfigStartupTask(Global global, IHostingEnvironment hostingEnvironment)
		{
			Global = global;
			WebsiteTorifier = new WebsiteTorifier(((HostingEnvironment)hostingEnvironment).WebRootPath);
		}

		public async Task ExecuteAsync(CancellationToken cancellationToken)
		{
			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));
			Logger.LogSoftwareStarted("Wasabi Backend");

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
			var configFilePath = Path.Combine(Global.DataDir, "Config.json");
			var config = new Config(configFilePath);
			await config.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo("Config is successfully initialized.");

			var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
			var roundConfig = new CcjRoundConfig(roundConfigFilePath);
			await roundConfig.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo("RoundConfig is successfully initialized.");

			string host = config.GetBitcoinCoreRpcEndPoint().ToString(config.Network.RPCPort);
			var rpc = new RPCClient(
					authenticationString: config.BitcoinRpcConnectionString,
					hostOrUri: host,
					network: config.Network);

			await Global.InitializeAsync(config, roundConfig, rpc);

			try
			{
				WebsiteTorifier.CloneAndUpdateOnionIndexHtml();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception);
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception);
		}
	}
}
