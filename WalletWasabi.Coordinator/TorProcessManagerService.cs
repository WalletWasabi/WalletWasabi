using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Tor;
using WalletWasabi.WabiSabi.Coordinator;

namespace WalletWasabi.Coordinator;

public class TorProcessManagerService(TorSettings torSettings, WabiSabiConfig config) : IHostedService
{
	private readonly TorProcessManager _torManager = new(torSettings, new EventBus());

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var (_, torControlClient) = await _torManager.StartAsync(attempts: 3, cancellationToken).ConfigureAwait(false);
		Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

		if (torControlClient is { } nonNullTorControlClient)
		{
			string onionServiceId;
			if (!string.IsNullOrWhiteSpace(config.OnionServicePrivateKey))
			{
				onionServiceId = await nonNullTorControlClient
					.CreateOnionServiceAsync(config.OnionServicePrivateKey, 80, 37126, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				(onionServiceId, var privateKey) = await nonNullTorControlClient
					.CreateKeylessOnionServiceAsync(80, 37126, cancellationToken).ConfigureAwait(false);
				config.OnionServicePrivateKey = privateKey;
				config.AnnouncerConfig.CoordinatorUri = $"http://{onionServiceId}.onion";
				config.AnnouncerConfig.ReadMoreUri = $"http://{onionServiceId}.onion";
				config.ToFile();
			}

			Logger.LogInfo($"Coordinator server listening on http://{onionServiceId}.onion");
		}
	}


	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await _torManager.DisposeAsync().ConfigureAwait(false);
	}
}
