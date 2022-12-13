using System.Linq;
using System.Collections.Immutable;
using WalletWasabi.Affiliation.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using WalletWasabi.Tor.Http;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

namespace WalletWasabi.Affiliation;

public class AffiliationManager : BackgroundService, IAffiliationManager
{
	public AffiliationManager(Arena arena, ImmutableDictionary<AffiliationFlag, string> urls, string privateKeyHex)
	{
		Signer = new(privateKeyHex);
		Arena = arena;
		HttpClient httpclient = new();
		Clients = urls.ToDictionary(x => x.Key, x => new AffiliateServerHttpApiClient(new ClearnetHttpClient(httpclient, () => new Uri(x.Value)))).ToImmutableDictionary();
		AffiliateServerStatusUpdater = new(Clients);
		CoinjoinRequestsUpdater = new(Arena, Clients, Signer);
	}

	private Signer Signer { get; }
	private Arena Arena { get; }
	private ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private AffiliateServerStatusUpdater AffiliateServerStatusUpdater { get; }
	private CoinjoinRequestsUpdater CoinjoinRequestsUpdater { get; }

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await AffiliateServerStatusUpdater.StartAsync(stoppingToken).ConfigureAwait(false);
		await CoinjoinRequestsUpdater.StartAsync(stoppingToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await AffiliateServerStatusUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
		await CoinjoinRequestsUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		AffiliateServerStatusUpdater.Dispose();
		Signer.Dispose();
	}

	public AffiliateInformation GetAffiliateInformation()
	{
		return new AffiliateInformation(AffiliateServerStatusUpdater.RunningAffiliateServers, CoinjoinRequestsUpdater.GetCoinjoinRequests());
	}
}
