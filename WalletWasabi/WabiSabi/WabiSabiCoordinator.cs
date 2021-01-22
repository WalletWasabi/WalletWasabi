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
using WalletWasabi.WabiSabi.Backend.Banning;

namespace WalletWasabi.WabiSabi
{
	public class WabiSabiCoordinator : BackgroundService
	{
		public WabiSabiCoordinator(CoordinatorParameters parameters)
		{
			DataDir = parameters.DataDir;
			WorkDir = Path.Combine(DataDir, "WabiSabi");
			IoHelpers.EnsureDirectoryExists(WorkDir);

			var configFilePath = Path.Combine(DataDir, "WabiSabiConfig.json");
			Config = new(configFilePath);
			Config.LoadOrCreateDefaultFile();
			ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => { });

			var prisonFilePath = Path.Combine(WorkDir, "Prison.txt");
			Judge = new(parameters.UtxoJudgementPeriod, prisonFilePath);
		}

		public string DataDir { get; }
		public string WorkDir { get; }
		public WabiSabiConfig Config { get; }
		public ConfigWatcher ConfigWatcher { get; }
		public Judge Judge { get; }

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
			await Judge.StartAsync(stoppingToken).ConfigureAwait(false);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken).ConfigureAwait(false);
			await ConfigWatcher.StopAsync(cancellationToken).ConfigureAwait(false);
			await Judge.StopAsync(cancellationToken).ConfigureAwait(false);
		}

		public override void Dispose()
		{
			ConfigWatcher.Dispose();
			Judge.Dispose();
			base.Dispose();
		}
	}
}
