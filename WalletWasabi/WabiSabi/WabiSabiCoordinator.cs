using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Backend;

namespace WalletWasabi.WabiSabi
{
	public class WabiSabiCoordinator : BackgroundService
	{
		public WabiSabiCoordinator(CoordinatorParameters parameters)
		{
			DataDir = parameters.DataDir;
			IoHelpers.EnsureDirectoryExists(DataDir);
			var configFilePath = Path.Combine(DataDir, "WabiSabiConfig.json");
			Config = new(configFilePath);
			Config.LoadOrCreateDefaultFile();
			Logger.LogInfo("WabiSabi configuration is successfully initialized.");

			ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => { });
		}

		public string DataDir { get; }
		public WabiSabiConfig Config { get; }
		public ConfigWatcher ConfigWatcher { get; }

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken).ConfigureAwait(false);
			await ConfigWatcher.StopAsync(cancellationToken).ConfigureAwait(false);
		}

		public override void Dispose()
		{
			ConfigWatcher.Dispose();
			base.Dispose();
		}
	}
}
