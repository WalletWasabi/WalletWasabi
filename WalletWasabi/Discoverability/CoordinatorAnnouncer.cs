using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NNostr.Client;
using NNostr.Client.Protocols;
using WalletWasabi.Bases;
using WalletWasabi.Logging;

namespace WalletWasabi.Discoverability;

public class CoordinatorAnnouncer(TimeSpan period, AnnouncerConfig config, Network network) : PeriodicRunner(period)
{
	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		using var client = NostrClientFactory.Create(config.RelayUris.Select(x => new Uri(x)).ToArray());
		var unsignedAnnouncementEvent = new NostrEvent
		{
			Kind = 15750,
			Content = config.CoordinatorDescription,
			Tags =
			[
				CreateTag("type", "wabisabi"),
				CreateTag("network", network.ChainName.ToString().ToLower()),
				CreateTag("endpoint", config.CoordinatorUri),
				CreateTag("coordinationfee", config.CoordinationFee.ToString(CultureInfo.InvariantCulture)),
				CreateTag("absolutemininputcount", config.AbsoluteMinInputCount.ToString(CultureInfo.InvariantCulture)),
				CreateTag("readmore", config.ReadMoreUri)
			]
		};

		using var key = config.Key.FromNIP19Nsec();
		var announcementEvent = await unsignedAnnouncementEvent.ComputeIdAndSignAsync(key).ConfigureAwait(false);

		using var timeoutCancellationTokenSource = new CancellationTokenSource (TimeSpan.FromSeconds(10));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);
		await client.PublishAsync([announcementEvent], linkedCts.Token).ConfigureAwait(false);

		Logger.LogInfo($"Coordinator has been successfully announced on Nostr ({announcementEvent.Id}).");
	}

	private static NostrEventTag CreateTag(string tagIdentifier, string data) =>
			new() { TagIdentifier = tagIdentifier, Data = [data] };
}
