using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Secp256k1;
using NNostr.Client;
using NNostr.Client.Protocols;
using WalletWasabi.Bases;
using WalletWasabi.Logging;

namespace WalletWasabi.Discoverability;

public class NostrCoordinatorPublisher : PeriodicRunner
{
	public NostrCoordinatorPublisher(TimeSpan period, AnnouncerConfig config, Network network) : base(period)
	{
		Config = config;
		Network = network;
		Key = config.Key.FromNIP19Nsec();
	}

	private AnnouncerConfig Config { get; }
	private Network Network { get; }
	private ECPrivKey Key { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		if (Network == Network.RegTest)
		{
			throw new NotSupportedException($"Coordinator publishing on Nostr is not supported on {Network}");
		}

		using var client = NostrExtensions.Create(Config.RelayUris.Select(x => new Uri(x)).ToArray(), (EndPoint?)null);
		var discoveryEvent = await CreateCoordinatorDiscoveryEventAsync(Config, Key, Network).ConfigureAwait(false);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancel);
		await client.PublishAsync([discoveryEvent], linkedCts.Token).ConfigureAwait(false);

		Logger.LogInfo("Coordinator has been successfully published on Nostr.");
	}

	private async Task<NostrEvent> CreateCoordinatorDiscoveryEventAsync(AnnouncerConfig config, ECPrivKey key, Network network)
	{
		var evt = new NostrEvent()
		{
			Kind = 15750,
			Content = config.CoordinatorDescription,
			Tags =
			[
				CreateTag("type", "wabisabi"),
				CreateTag("network", network.ChainName.ToString().ToLower()),
				CreateTag("endpoint", config.CoordinatorUri),
				CreateTag("coordinatorfee", config.CoordinatorFee.ToString(CultureInfo.InvariantCulture)),
				CreateTag("absolutemininputcount", config.AbsoluteMinInputCount.ToString(CultureInfo.InvariantCulture)),
				CreateTag("readmore", config.ReadMoreUri)
			]
		};

		await evt.ComputeIdAndSignAsync(key).ConfigureAwait(false);
		return evt;
	}

	private static NostrEventTag CreateTag(string tagIdentifier, string data)
	{
		return new() { TagIdentifier = tagIdentifier, Data = [data] };
	}

	public override void Dispose()
	{
		Key.Dispose();
		base.Dispose();
	}
}
