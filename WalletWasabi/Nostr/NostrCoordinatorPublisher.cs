using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Secp256k1;
using NNostr.Client;
using WalletWasabi.Bases;
using WalletWasabi.Logging;

namespace WalletWasabi.Nostr;

public class NostrCoordinatorPublisher : PeriodicRunner
{
	public NostrCoordinatorPublisher(TimeSpan period, ECPrivKey key, NostrCoordinatorConfiguration coordinatorConfiguration, Uri[] relayUris) : base(period)
	{
		Key = key;
		CoordinatorConfiguration = coordinatorConfiguration;
		Client = NostrExtensions.Create(relayUris, (EndPoint?)null);
	}

	private INostrClient Client { get; }

	private NostrCoordinatorConfiguration CoordinatorConfiguration { get; }

	private ECPrivKey Key { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var discoveryEvent = await CoordinatorConfiguration.CreateCoordinatorDiscoveryEventAsync(Key).ConfigureAwait(false);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancel);
		await Client.PublishAsync([discoveryEvent], linkedCts.Token).ConfigureAwait(false);

		Logger.LogInfo("Coordinator has been successfully published on Nostr.");
	}
}
