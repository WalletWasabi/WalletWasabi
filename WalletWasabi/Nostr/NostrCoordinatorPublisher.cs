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
	private readonly Uri[] _relayUris = [new("wss://relay.primal.net")];

	public NostrCoordinatorPublisher(TimeSpan period, ECPrivKey key, NostrCoordinatorConfiguration coordinatorConfiguration) : base(period)
	{
		CoordinatorConfiguration = coordinatorConfiguration;
		Key = key;
		Client = NostrExtensions.Create(_relayUris, (EndPoint?)null);
	}

	private INostrClient Client { get; }

	private NostrCoordinatorConfiguration CoordinatorConfiguration { get; }

	private ECPrivKey Key { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		try
		{
			var discoveryEvent = await Key.CreateCoordinatorDiscoveryEventAsync(CoordinatorConfiguration).ConfigureAwait(false);

			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancel);
			await Client.PublishAsync([discoveryEvent], linkedCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError(ex);
		}
	}

	public override void Dispose()
	{
		Key.Dispose();
		base.Dispose();
	}
}
