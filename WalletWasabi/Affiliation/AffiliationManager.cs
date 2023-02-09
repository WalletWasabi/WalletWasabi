using System.Linq;
using System.Collections.Immutable;
using System.Net.Http;
using WalletWasabi.Affiliation.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using Microsoft.Extensions.Hosting;
using WalletWasabi.WabiSabi.Backend;

namespace WalletWasabi.Affiliation;

public class AffiliationManager : BackgroundService
{
	public AffiliationManager(Arena arena, WabiSabiConfig wabiSabiConfig, IHttpClientFactory httpClientFactory)
	{
		Signer = new(wabiSabiConfig.AffiliationMessageSignerKey);
		Arena = arena;
		Clients = wabiSabiConfig.AffiliateServers.ToDictionary(
			 x => x.Key,
			  x =>
			  {
				  string affiliationFlagName = x.Key.Name;
				  HttpClient httpClient = httpClientFactory.CreateClient("AffiliateHttpClient-" + affiliationFlagName);
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
		await CoinjoinRequestsUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
		await AffiliateServerStatusUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
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
