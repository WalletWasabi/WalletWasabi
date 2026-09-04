using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NNostr.Client;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.WebClients;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WebClients;

public class WasabiNostrClientTests
{
	[Fact]
	public async Task IgnoresEventsFromUnknownPubkeysAsync()
	{
		// Arrange
		var unknownPubkey = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234";

		using var nostrClient = new TesteabletNostrClient([], manualMode: true);
		using var wasabiClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);

		await wasabiClient.ConnectAndSubscribeAsync(CancellationToken.None);

		// Act - simulate receiving an event from an unknown pubkey
		var eventFromUnknown = new NostrEvent
		{
			Id = "event1",
			PublicKey = unknownPubkey,
			Tags = [new NostrEventTag { TagIdentifier = "version", Data = ["99.0.0"] }]
		};

		nostrClient.SimulateEventsReceived([eventFromUnknown]);
		nostrClient.SimulateEoseReceived();

		// Assert - channel should be completed with no items
		var releases = new List<ReleaseInfo>();
		await foreach (var release in wasabiClient.EventsReader.ReadAllAsync())
		{
			releases.Add(release);
		}

		Assert.Empty(releases);
	}

	[Fact]
	public async Task AcceptsEventsFromWasabiTeamPubkeyAsync()
	{
		// Arrange
		using var nostrClient = new TesteabletNostrClient([], manualMode: true);
		using var wasabiClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);

		await wasabiClient.ConnectAndSubscribeAsync(CancellationToken.None);

		// Act - simulate receiving an event from the Wasabi team pubkey
		var eventFromWasabi = new NostrEvent
		{
			Id = "event1",
			PublicKey = TesteabletNostrClient.WasabiTeamPubKeyHex,
			Tags = [new NostrEventTag { TagIdentifier = "version", Data = ["2.5.0"] }]
		};

		nostrClient.SimulateEventsReceived([eventFromWasabi]);
		nostrClient.SimulateEoseReceived();

		// Assert - should have received the release info
		var releases = new List<ReleaseInfo>();
		await foreach (var release in wasabiClient.EventsReader.ReadAllAsync())
		{
			releases.Add(release);
		}

		Assert.Single(releases);
		Assert.Equal(new Version(2, 5, 0), releases[0].Version);
	}

	[Fact]
	public async Task FiltersOutEventsFromWrongPubkeyWhileAcceptingValidOnesAsync()
	{
		// Arrange
		var unknownPubkey = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234";

		using var nostrClient = new TesteabletNostrClient([], manualMode: true);
		using var wasabiClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);

		await wasabiClient.ConnectAndSubscribeAsync(CancellationToken.None);

		// Act - simulate receiving mixed events: one from unknown, one from Wasabi team
		var eventFromUnknown = new NostrEvent
		{
			Id = "malicious-event",
			PublicKey = unknownPubkey,
			Tags = [new NostrEventTag { TagIdentifier = "version", Data = ["99.0.0"] }]
		};

		var eventFromWasabi = new NostrEvent
		{
			Id = "legitimate-event",
			PublicKey = TesteabletNostrClient.WasabiTeamPubKeyHex,
			Tags = [new NostrEventTag { TagIdentifier = "version", Data = ["2.6.0"] }]
		};

		nostrClient.SimulateEventsReceived([eventFromUnknown, eventFromWasabi]);
		nostrClient.SimulateEoseReceived();

		// Assert - should only have the legitimate release
		var releases = new List<ReleaseInfo>();
		await foreach (var release in wasabiClient.EventsReader.ReadAllAsync())
		{
			releases.Add(release);
		}

		Assert.Single(releases);
		Assert.Equal(new Version(2, 6, 0), releases[0].Version);
	}
}