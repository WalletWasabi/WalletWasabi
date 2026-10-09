using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using NBitcoin;
using Nostra;
using Nostra.CSharp;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WebClients;
using Xunit;
using static WalletWasabi.Discoverability.NostrExtensions;
using static WalletWasabi.Services.Workers;
using Shareable = Nostra.ShareableModule;
using SecretKey = Nostra.SecretKeyModule;
using Event = Nostra.EventModule;
using SubscriptionFilter = Nostra.Client.SubscriptionFilter;

namespace WalletWasabi.Tests.UnitTests.Services;

public class UpdateManagerTests
{
	[Fact]
	public async Task NewReleaseDetectedAsync()
	{
		// Arrange
		var emptyTags = ImmutableDictionary<string, Uri>.Empty;
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TestableNostrClient([
			new ReleaseInfo(new Version(1, 0, 0), emptyTags),
			new ReleaseInfo(new Version(3, 5, 8), emptyTags),
			new ReleaseInfo(new Version(2, 5, 1), emptyTags)
		]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus, currentVersion: new Version(1, 0, 0), nostrPubKey: TestableNostrClient.TestNPub);

		// Act
		var updateStatusObtainedTask = new TaskCompletionSource<UpdateManager.UpdateStatus>();
		using var subscription =
			eventBus.Subscribe<NewSoftwareVersionAvailable>(e => updateStatusObtainedTask.SetResult(e.UpdateStatus));

		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);
		var updateStatusReceived = await updateStatusObtainedTask.Task.WaitAsync(cts.Token);
		await updateTask;

		// Assert
		Assert.Equal(Version.Parse("3.5.8"), updateStatusReceived.ClientVersion);
		Assert.False(updateStatusReceived.ClientUpToDate);
		Assert.False(updateStatusReceived.IsReadyToInstall);
	}

	[Fact]
	public async Task MultipleNewerReleaseDetectedAsync()
	{
		// Arrange
		var emptyTags = ImmutableDictionary<string, Uri>.Empty;
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TestableNostrClient([
			new ReleaseInfo(new Version(1, 0, 0), emptyTags),
			new ReleaseInfo(new Version(3, 5, 8), emptyTags),
			new ReleaseInfo(new Version(3, 4, 0), emptyTags)
		]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus, currentVersion: new Version(1, 0, 0), nostrPubKey: TestableNostrClient.TestNPub);

		// Act
		var updateStatusObtainedTask = new TaskCompletionSource<UpdateManager.UpdateStatus>();
		using var subscription =
			eventBus.Subscribe<NewSoftwareVersionAvailable>(e => updateStatusObtainedTask.SetResult(e.UpdateStatus));

		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);
		var updateStatusReceived = await updateStatusObtainedTask.Task.WaitAsync(cts.Token);
		await updateTask;

		// Assert
		Assert.Equal(Version.Parse("3.5.8"), updateStatusReceived.ClientVersion);
		Assert.False(updateStatusReceived.ClientUpToDate);
		Assert.False(updateStatusReceived.IsReadyToInstall);
	}

	[Fact]
	public async Task OnlyOldReleasesFoundAsync()
	{
		// Arrange
		var emptyTags = ImmutableDictionary<string, Uri>.Empty;
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TestableNostrClient([
			new ReleaseInfo(new Version(0, 1, 0), emptyTags),
			new ReleaseInfo(new Version(2, 5, 0), emptyTags),
			new ReleaseInfo(new Version(2, 5, 1), emptyTags)
		]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus, nostrPubKey: TestableNostrClient.TestNPub);

		// Act
		var updateStatusObtainedTask = new TaskCompletionSource<UpdateManager.UpdateStatus>();
		using var subscription =
			eventBus.Subscribe<NewSoftwareVersionAvailable>(e => updateStatusObtainedTask.SetException(new Exception("Unexpected event. This should have never been called. Bug")));

		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await updateStatusObtainedTask.Task.WaitAsync(cts.Token));

		await updateTask;
	}

	[Fact]
	public async Task NothingFoundAsync()
	{
		// Arrange
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TestableNostrClient([]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus, nostrPubKey: TestableNostrClient.TestNPub);

		// Act
		var updateStatusObtainedTask = new TaskCompletionSource<UpdateManager.UpdateStatus>();
		using var subscription =
			eventBus.Subscribe<NewSoftwareVersionAvailable>(e => updateStatusObtainedTask.SetException(new Exception("Unexpected event. This should have never been called. Bug")));

		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await updateStatusObtainedTask.Task.WaitAsync(cts.Token));

		await updateTask;
	}

	[Fact]
	public async Task EmptyRelayResponseCompletesUpdateCheckAsync()
	{
		// Arrange
		var eventBus = new EventBus();
		// this nostr client doesn't return any event
		var nostrClientFactory = () => new TestableNostrClient([], sendEventsReceived: false);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus, nostrPubKey: TestableNostrClient.TestNPub);

		// Act
		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);

		// Assert - should complete quickly without timing out
		await updateTask.WaitAsync(TimeSpan.FromSeconds(1));
	}
}

public class TestableNostrClient : INostrClient
{
	public static readonly AuthorIdT WasabiTeamPubKey = Shareable.FromNPub.Invoke(Constants.WasabiTeamNostrPubKey);

	// Test secret key for signing events in tests
	private static readonly SecretKeyT TestSecretKey = SecretKey.CreateRandom();
	public static readonly AuthorIdT TestPubKey = SecretKey.getPubKey(TestSecretKey);
	public static readonly string TestNPub = Shareable.ToNPub(TestPubKey);

	private readonly ReleaseInfo[] _releases;
	private readonly bool _sendEventsReceived;
	private readonly bool _manualMode;
	private readonly SecretKeyT _secretKey;
	private string? _activeSubscriptionId;
	private Action<object, RelayMessageResult>? _onMessage;

	public TestableNostrClient(ReleaseInfo[] releases, bool sendEventsReceived = true, SecretKeyT? secretKey = null, bool manualMode = false)
	{
		_releases = releases;
		_sendEventsReceived = sendEventsReceived;
		_manualMode = manualMode;
		_secretKey = secretKey ?? TestSecretKey;
	}

	public void SimulateEventsReceived(EventT[] events)
	{
		if (_activeSubscriptionId is null || _onMessage is null)
		{
			throw new InvalidOperationException("No active subscription.");
		}

		foreach (var evt in events)
		{
			_onMessage(this, new RelayMessageResult.Event(_activeSubscriptionId, evt));
		}
	}

	public void SimulateEoseReceived()
	{
		if (_activeSubscriptionId is null || _onMessage is null)
		{
			throw new InvalidOperationException("No active subscription.");
		}
		_onMessage(this, new RelayMessageResult.EndOfStoredEvents(_activeSubscriptionId));
	}

	public void Dispose()
	{
	}

	public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public void Subscribe(string subscriptionId, SubscriptionFilter filter)
	{
		_activeSubscriptionId = subscriptionId;
	}

	public void Publish(EventT signedEvent)
	{
	}

	public Task StartListeningAsync(Action<object, RelayMessageResult> onMessage, Action<string>? onError, CancellationToken cancellationToken)
	{
		_onMessage = onMessage;

		if (_manualMode)
		{
			return Task.CompletedTask;
		}

		if (_activeSubscriptionId is null)
		{
			return Task.CompletedTask;
		}

		if (_sendEventsReceived)
		{
			foreach (var (release, i) in _releases.Select((r, i) => (r, i)))
			{
				var tags = ListModule.OfSeq([CreateTag("version", release.Version.ToString())]);

				var unsignedEvent = Event.Create(Kind.Text, tags, "");
				var signedEvent = Event.Sign(_secretKey, unsignedEvent);

				onMessage(this, new RelayMessageResult.Event(_activeSubscriptionId, signedEvent));
			}
		}

		onMessage(this, new RelayMessageResult.EndOfStoredEvents(_activeSubscriptionId));
		return Task.CompletedTask;
	}
}

public class RoundStateUpdaterForTesting
{
	public static MailboxProcessor<RoundUpdateMessage> Create(IWabiSabiApiRequestHandler api, CancellationToken? cancellationToken = null) =>
		Create(api, cancellationToken, autoUpdate: true);

	public static MailboxProcessor<RoundUpdateMessage> CreateManual(IWabiSabiApiRequestHandler api, CancellationToken? cancellationToken = null) =>
		Create(api, cancellationToken, autoUpdate: false);

	private static MailboxProcessor<RoundUpdateMessage> Create(IWabiSabiApiRequestHandler api, CancellationToken? cancellationToken, bool autoUpdate) =>
		Spawn<RoundUpdateMessage>($"RoundStateUpdater-{Random.Shared.Next()}", async (mailbox, token) =>
		{
			// Stop the ticker when cancellation or disposal ends the worker.
			await using var ticker = autoUpdate
				? new Timer(_ => mailbox.Post(new RoundUpdateMessage.UpdateMessage(DateTime.UtcNow)), null, TimeSpan.Zero, TimeSpan.FromSeconds(1))
				: null;
			var process = EventDriven(
				new RoundsState(DateTime.UtcNow, TimeSpan.Zero, new Dictionary<uint256, RoundState>(), ImmutableList<RoundStateAwaiter>.Empty),
				RoundStateUpdater.Create(api));
			await process(mailbox, token).ConfigureAwait(false);
		}, cancellationToken);
}
