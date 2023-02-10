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
				  HttpClient httpClient = httpClientFactory.CreateClient("AffiliateHttpClient");
				  ClearnetHttpClient client = new(httpClient, baseUriGetter: () => new Uri(x.Value));
				  return new AffiliateServerHttpApiClient(client);
			  }).ToImmutableDictionary();
		AffiliateServerStatusUpdater = new(Clients);
		CoinJoinRequestsUpdater = new(Arena, Clients, Signer);
	}

	private AffiliationMessageSigner Signer { get; }
	private Arena Arena { get; }
	private ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private AffiliateServerStatusUpdater AffiliateServerStatusUpdater { get; }
	private CoinJoinRequestsUpdater CoinJoinRequestsUpdater { get; }

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await AffiliateServerStatusUpdater.StartAsync(stoppingToken).ConfigureAwait(false);
		await CoinJoinRequestsUpdater.StartAsync(stoppingToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await CoinJoinRequestsUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
		await AffiliateServerStatusUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		CoinJoinRequestsUpdater.Dispose();
		Signer.Dispose();
	}

	public AffiliateInformation GetAffiliateInformation()
	{
		return new AffiliateInformation(AffiliateServerStatusUpdater.GetRunningAffiliateServers(), CoinJoinRequestsUpdater.GetCoinjoinRequests());
	}
}
