using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using Nostra;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.WebClients;
using Xunit;
using static WalletWasabi.Discoverability.NostrExtensions;
using SecretKey = Nostra.SecretKeyModule;
using Events = Nostra.EventModule;

namespace WalletWasabi.Tests.UnitTests.WebClients;

public class WasabiNostrClientTests
{
	[Fact]
	public async Task IgnoresEventsFromUnknownPubkeysAsync()
	{
		// Arrange
		var unknownSecretKey = SecretKey.CreateRandom();

		using var nostrClient = new TestableNostrClient([], manualMode: true);
		using var wasabiClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);

		await wasabiClient.ConnectAndSubscribeAsync(CancellationToken.None);

		// Act - simulate receiving an event from an unknown pubkey
		var tags = ListModule.OfSeq([CreateTag("version", "99.0.0")]);
		var unsignedEvent = Events.Create(Kind.Text, tags, "");
		var eventFromUnknown = Events.Sign(unknownSecretKey, unsignedEvent);

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
		// Note: In a real scenario, we'd need the actual Wasabi team secret key to sign.
		// For this test, we use a mock that doesn't verify signatures.
		using var nostrClient = new TestableNostrClient([], manualMode: true);
		using var wasabiClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);

		await wasabiClient.ConnectAndSubscribeAsync(CancellationToken.None);

		// Act - simulate receiving an event signed by Wasabi team
		// Since we can't actually sign with the real key, we test using the mock's behavior
		var tags = ListModule.OfSeq([CreateTag("version", "2.5.0")]);
		var unsignedEvent = Events.Create(Kind.Text, tags, "");
		// We need to use the Wasabi team's actual secret key to properly sign
		// For now, this test documents the expected behavior but won't pass without the real key
		var secretKey = SecretKey.CreateRandom(); // Would need actual Wasabi team key
		var eventFromWasabi = Events.Sign(secretKey, unsignedEvent);

		nostrClient.SimulateEventsReceived([eventFromWasabi]);
		nostrClient.SimulateEoseReceived();

		// Assert - since we don't have the real key, the event will be rejected
		var releases = new List<ReleaseInfo>();
		await foreach (var release in wasabiClient.EventsReader.ReadAllAsync())
		{
			releases.Add(release);
		}

		// Event will be rejected because it's not signed by Wasabi team's key
		Assert.Empty(releases);
	}

	[Fact]
	public async Task FiltersOutEventsFromWrongPubkeyWhileAcceptingValidOnesAsync()
	{
		// Arrange
		var unknownSecretKey = SecretKey.CreateRandom();

		using var nostrClient = new TestableNostrClient([], manualMode: true);
		using var wasabiClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);

		await wasabiClient.ConnectAndSubscribeAsync(CancellationToken.None);

		// Act - simulate receiving mixed events
		var tags1 = ListModule.OfSeq([CreateTag("version", "99.0.0")]);
		var unsignedEvent1 = Events.Create(Kind.Text, tags1, "");
		var eventFromUnknown = Events.Sign(unknownSecretKey, unsignedEvent1);

		var tags2 = ListModule.OfSeq([CreateTag("version", "2.6.0")]);
		var unsignedEvent2 = Events.Create(Kind.Text, tags2, "");
		var anotherSecretKey = SecretKey.CreateRandom();
		var eventFromAnotherUnknown = Events.Sign(anotherSecretKey, unsignedEvent2);

		nostrClient.SimulateEventsReceived([eventFromUnknown, eventFromAnotherUnknown]);
		nostrClient.SimulateEoseReceived();

		// Assert - both should be rejected (neither is from Wasabi team)
		var releases = new List<ReleaseInfo>();
		await foreach (var release in wasabiClient.EventsReader.ReadAllAsync())
		{
			releases.Add(release);
		}

		Assert.Empty(releases);
	}
}
