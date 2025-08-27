using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Tor;

namespace WalletWasabi.Coordinator;

public class TorProcessManagerService(TorSettings torSettings) : IHostedService
{
	private readonly TorProcessManager _torManager = new(torSettings, new EventBus());

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var (_, torControlClient) = await _torManager.StartAsync(attempts: 3, cancellationToken).ConfigureAwait(false);
		Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

		if (torControlClient is { } nonNullTorControlClient)
		{
			var keyFilePath = Path.Combine(torSettings.TorDataDir, "onion-service-private-key");
			var onionServiceId = "";
			if (File.Exists(keyFilePath))
			{
				var onionServicePrivateKey = await File.ReadAllTextAsync(keyFilePath, cancellationToken);
				onionServiceId = await nonNullTorControlClient
					.CreateOnionServiceAsync(onionServicePrivateKey, 80, 37126, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				(onionServiceId, var privateKey) = await nonNullTorControlClient
					.CreateKeylessOnionServiceAsync(80, 37126, cancellationToken).ConfigureAwait(false);
				await File.WriteAllTextAsync(keyFilePath, privateKey, cancellationToken).ConfigureAwait(false);
			}

			var OnionServiceUri = new Uri($"http://{onionServiceId}.onion");
			Logger.LogInfo($"Coordinator server listening on {OnionServiceUri}");
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await _torManager.DisposeAsync().ConfigureAwait(false);
	}
}
