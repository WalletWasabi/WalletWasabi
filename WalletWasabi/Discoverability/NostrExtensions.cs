using System.Net;
using Microsoft.FSharp.Collections;
using WalletWasabi.WebClients;

namespace WalletWasabi.Discoverability;

public static class NostrClientFactory
{
	public static INostrClient Create(Uri[] relays)
	{
		return relays.Length switch
		{
			0 => throw new ArgumentException("At least one relay is required.", nameof(relays)),
			_ => new CompositeNostrClient(relays)
		};
	}

	public static INostrClient Create(Uri[] relays, EndPoint? proxyEndpoint)
	{
		// TODO: Implement proxy support once Nostra library adds it
		// For now, proxy parameter is ignored
		return Create(relays);
	}
}

public static class NostrExtensions
{
	public static Tag CreateTag(string key, params string[] values) =>
		Tags.Create(key, ListModule.OfSeq(values));

	public static async Task PublishAsync(
		this INostrClient client,
		Event[] events,
		CancellationToken cancellationToken)
	{
		await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

		foreach (var evt in events)
		{
			client.Publish(evt);
		}

		// Give some time for the events to be sent
		await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

		await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
	}
}
