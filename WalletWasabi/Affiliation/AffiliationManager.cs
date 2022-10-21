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

public class AffiliationManager : BackgroundService
{
	public AffiliationManager(Arena arena, ImmutableDictionary<AffiliationFlag, string> urls, string privateKeyHex)
	{
		Arena = arena;
		HttpClient httpclient = new();
		Clients = urls.ToDictionary(x => x.Key, x => new AffiliateServerHttpApiClient(new ClearnetHttpClient(httpclient, () => new Uri(x.Value)))).ToImmutableDictionary();
		AffiliateServerStatusUpdater = new(Clients);
		AffiliateInformationUpdater = new(Arena, Clients, new(privateKeyHex));
	}

	private Arena Arena { get; }
	private ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private AffiliateServerStatusUpdater AffiliateServerStatusUpdater { get; }
	private AffiliateInformationUpdater AffiliateInformationUpdater { get; }

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await AffiliateServerStatusUpdater.StartAsync(stoppingToken).ConfigureAwait(false);
		await AffiliateInformationUpdater.StartAsync(stoppingToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await AffiliateServerStatusUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
		await AffiliateInformationUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		AffiliateServerStatusUpdater.Dispose();
	}

	public AffiliateInformation GetAffiliateInformation()
	{
		return new AffiliateInformation(AffiliateServerStatusUpdater.RunningAffiliateServers, AffiliateInformationUpdater.GetPaymentData());
	}
}
