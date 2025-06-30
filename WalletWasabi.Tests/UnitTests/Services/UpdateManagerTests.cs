using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NNostr.Client;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WebClients;
using Xunit;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Tests.UnitTests.Services;

public class UpdateManagerTests
{
	[Fact]
	public async Task NewReleaseDetected()
	{
		// Arrange
		var emptyTags = ImmutableDictionary<string, Uri>.Empty;
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TesteabletNostrClient([
			new ReleaseInfo(new Version(1, 0, 0), emptyTags),
			new ReleaseInfo(new Version(3, 5, 8), emptyTags),
			new ReleaseInfo(new Version(2, 5, 1), emptyTags)
		]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus);

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
	public async Task MultipleNewerReleaseDetected()
	{
		// Arrange
		var emptyTags = ImmutableDictionary<string, Uri>.Empty;
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TesteabletNostrClient([
			new ReleaseInfo(new Version(1, 0, 0), emptyTags),
			new ReleaseInfo(new Version(3, 5, 8), emptyTags),
			new ReleaseInfo(new Version(3, 4, 0), emptyTags)
		]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus);

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
	public async Task OnlyOldReleasesFound()
	{
		// Arrange
		var emptyTags = ImmutableDictionary<string, Uri>.Empty;
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TesteabletNostrClient([
			new ReleaseInfo(new Version(0, 1, 0), emptyTags),
			new ReleaseInfo(new Version(2, 5, 0), emptyTags),
			new ReleaseInfo(new Version(2, 5, 1), emptyTags)
		]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus);

		// Act
		var updateStatusObtainedTask = new TaskCompletionSource<UpdateManager.UpdateStatus>();
		using var subscription =
			eventBus.Subscribe<NewSoftwareVersionAvailable>(e => updateStatusObtainedTask.SetException(new Exception("Unexpected event. This should have never been called. Bug")));

		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await updateStatusObtainedTask.Task.WaitAsync(cts.Token));

		await updateTask;
	}

	[Fact]
	public async Task NothingFound()
	{
		// Arrange
		var eventBus = new EventBus();
		var nostrClientFactory = () => new TesteabletNostrClient([]);
		AsyncReleaseDownloader doNothingDownloader = (_, _) => Task.CompletedTask;

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var updaterFunc = UpdateManager.CreateUpdater(nostrClientFactory, doNothingDownloader, eventBus);

		// Act
		var updateStatusObtainedTask = new TaskCompletionSource<UpdateManager.UpdateStatus>();
		using var subscription =
			eventBus.Subscribe<NewSoftwareVersionAvailable>(e => updateStatusObtainedTask.SetException(new Exception("Unexpected event. This should have never been called. Bug")));

		var updateTask = updaterFunc(new UpdateManager.UpdateMessage(), Unit.Instance, cts.Token);
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await updateStatusObtainedTask.Task.WaitAsync(cts.Token));

		await updateTask;
	}
}

public class TesteabletNostrClient : INostrClient
{
	private readonly ReleaseInfo[] _releases;

	public TesteabletNostrClient(ReleaseInfo[] releases)
	{
		_releases = releases;
	}

	public void Dispose()
	{
	}

	public Task Disconnect() => Task.CompletedTask;

	public Task Connect(CancellationToken token) => Task.CompletedTask;

	public IAsyncEnumerable<string> ListenForRawMessages() => Enumerable.Empty<string>().ToAsyncEnumerable();

	public Task ListenForMessages() => Task.CompletedTask;

	public Task PublishEvent(NostrEvent nostrEvent, CancellationToken token) => Task.CompletedTask;

	public Task CloseSubscription(string subscriptionId, CancellationToken token) => Task.CompletedTask;

	public Task CreateSubscription(string subscriptionId, NostrSubscriptionFilter[] filters, CancellationToken token)
	{
		var nostrEvents = _releases
			.Select((r, i) => new NostrEvent
			{
				Id = i.ToString(),
				Tags = [ new NostrEventTag{ TagIdentifier = "version", Data = [r.Version.ToString()] }]
			}).ToArray();

		EventsReceived?.Invoke(this, (subscriptionId, nostrEvents));
		EoseReceived?.Invoke(this, subscriptionId);
		return Task.CompletedTask;
	}

	public Task ConnectAndWaitUntilConnected(CancellationToken connectionCancellationToken,
		CancellationToken lifetimeCancellationToken) => Task.CompletedTask;

	public event EventHandler<string>? MessageReceived;
	public event EventHandler<string>? InvalidMessageReceived;
	public event EventHandler<string>? NoticeReceived;
	public event EventHandler<(string subscriptionId, NostrEvent[] events)>? EventsReceived;
	public event EventHandler<(string eventId, bool success, string messafe)>? OkReceived;
	public event EventHandler<string>? EoseReceived;
}


