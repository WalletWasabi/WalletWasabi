using System.Globalization;
using Nostra;
using WalletWasabi.Bases;
using static WalletWasabi.Discoverability.NostrExtensions;
namespace WalletWasabi.Discoverability;


public class CoordinatorAnnouncer(AnnouncerConfig config, Network network, TimeSpan? time = null)
	: PeriodicRunner(time ?? TimeSpan.FromMinutes(15))
{
	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		using var client = NostrClientFactory.Create(config.RelayUris.Select(x => new Uri(x)).ToArray());

		Tag[] tags = [
			CreateTag("name", config.CoordinatorName),
			CreateTag("type", "wabisabi"),
			CreateTag("network", network.ChainName.ToString().ToLower()),
			CreateTag("endpoint", config.CoordinatorUri),
			CreateTag("absolutemininputcount", config.AbsoluteMinInputCount.ToString(CultureInfo.InvariantCulture)),
			CreateTag("readmore", config.ReadMoreUri)
		];

		var unsignedEvent = Events.Create((Kind)15750, tags, config.CoordinatorDescription);
		var secretKey = Shareable.FromNSec(config.Key);
		var announcementEvent = Events.Sign(secretKey, unsignedEvent);

		using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);
		await client.PublishAsync([announcementEvent], linkedCts.Token).ConfigureAwait(false);

		var eventIdHex = EventIds.ToHex(announcementEvent.Id);
		Logger.LogInfo($"Coordinator has been successfully announced on Nostr ({eventIdHex}).");
	}
}
