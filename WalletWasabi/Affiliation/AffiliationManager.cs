using System.Linq;
using System.Collections.Immutable;
using System.Net.Http;
using WalletWasabi.Affiliation.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

namespace WalletWasabi.Affiliation;

public class AffiliationManager : BackgroundService, IDisposable
{
	public AffiliationManager(Arena arena, IReadOnlyDictionary<AffiliationFlag, string> urls, string privateKeyHex, IHttpClientFactory httpClientFactory)
	{
		Signer = new(privateKeyHex);
		Arena = arena;
		Clients = urls.ToDictionary(
			 x => x.Key,
			  x =>
			  {
				  HttpClient httpClient = httpClientFactory.CreateClient("AffiliateHttpClient");
				  ClearnetHttpClient client = new(httpClient, baseUriGetter: () => new Uri(x.Value));
				  return new AffiliateServerHttpApiClient(client);
			  }).ToImmutableDictionary();
		AffiliateServerStatusUpdater = new(Clients);
		CoinjoinRequestsUpdater = new(Arena, Clients, Signer);
	}

	private AffiliationMessageSigner Signer { get; }
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
		CoinjoinRequestsUpdater.Dispose();
		Signer.Dispose();
	}

	public AffiliateInformation GetAffiliateInformation()
	{
		return new AffiliateInformation(AffiliateServerStatusUpdater.GetRunningAffiliateServers(), CoinjoinRequestsUpdater.GetCoinjoinRequests());
	}
}
