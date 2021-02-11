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
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi
{
	public class WabiSabiCoordinator : BackgroundService
	{
		public WabiSabiCoordinator(CoordinatorParameters parameters)
		{
			Parameters = parameters;

			Warden = new(parameters.UtxoWardenPeriod, parameters.PrisonFilePath, Config);
			ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => Logger.LogInfo("WabiSabi configuration has changed."));

			Rounds = new();
			Postman = new(Config, Prison, Rounds);
		}

		public ConfigWatcher ConfigWatcher { get; }
		public Warden Warden { get; }

		public CoordinatorParameters Parameters { get; }
		public PostRequestHandler Postman { get; }
		public Arena Rounds { get; }

		public string WorkDir => Parameters.CoordinatorDataDir;
		public Prison Prison => Warden.Prison;
		public WabiSabiConfig Config => Parameters.RuntimeCoordinatorConfig;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
			await Warden.StartAsync(stoppingToken).ConfigureAwait(false);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await Postman.DisposeAsync().ConfigureAwait(false);
			await base.StopAsync(cancellationToken).ConfigureAwait(false);
			await ConfigWatcher.StopAsync(cancellationToken).ConfigureAwait(false);
			await Warden.StopAsync(cancellationToken).ConfigureAwait(false);
		}

		public override void Dispose()
		{
			ConfigWatcher.Dispose();
			Warden.Dispose();
			base.Dispose();
		}
	}
}
