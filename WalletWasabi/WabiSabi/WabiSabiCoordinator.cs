using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.Utils;

namespace WalletWasabi.WabiSabi
{
	public class WabiSabiCoordinator : BackgroundService
	{
		public WabiSabiCoordinator(CoordinatorParameters parameters, IRPCClient rpc)
		{
			Parameters = parameters;

			Warden = new(parameters.UtxoWardenPeriod, parameters.PrisonFilePath, Config);
			ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => Logger.LogInfo("WabiSabi configuration has changed."));

			CoinJoinTransactionArchiver transactionArchiver = new(Path.Combine(parameters.CoordinatorDataDir, "CoinJoinTransactions"));
			Arena = new(parameters.RoundProgressSteppingPeriod, rpc.Network, Config, rpc, Warden.Prison, transactionArchiver);
		}

		public ConfigWatcher ConfigWatcher { get; }
		public Warden Warden { get; }

		public CoordinatorParameters Parameters { get; }
		public Arena Arena { get; }

		public WabiSabiConfig Config => Parameters.RuntimeCoordinatorConfig;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
			await Warden.StartAsync(stoppingToken).ConfigureAwait(false);
			await Arena.StartAsync(stoppingToken).ConfigureAwait(false);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken).ConfigureAwait(false);

			await Arena.StopAsync(cancellationToken).ConfigureAwait(false);
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
