using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control;

/// <summary>
/// Tests for <see cref="TorControlClient"/>.
/// </summary>
public class TorControlClientTests
{
	/// <summary>
	/// Verifies that client receives correct async events from Tor.
	/// </summary>
	[Fact(Timeout = 180000)] // 3 minutes
	public async Task ReceiveTorAsyncEventsUsingForeachAsync()
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		// Test parameters.
		const int ExpectedEventsNo = 3;
		const string AsyncEventContent = "CIRC 1000 EXTENDED moria1,moria2";

		var toServer = new Pipe();
		var toClient = new Pipe();

		// Set up Tor control client.
		await using var client = new TorControlClient(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		// Subscribe to Tor events.
		var events = client.ReadEventsAsync(timeoutCts.Token);

		// Send a Tor event to all subscribed clients (only one here).
		// This must happen after a client is subscribed.
		var serverTask = Task.Run(async () =>
		{
			// We do not want to send the data until the client is really subscribed.
			while (!timeoutCts.IsCancellationRequested)
			{
				if (client.SubscriberCount == 1)
				{
					break;
				}

				await Task.Delay(200).ConfigureAwait(false);
			}

			for (int i = 0; i < ExpectedEventsNo; i++)
			{
				Logger.LogTrace($"Server: Send async Tor event (#{i}): '650 {AsyncEventContent}'.");
				await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);
			}
		});

		// Iterate received events.
		int counter = 0;

		// Client should get all the events.
		await foreach (var receivedEvent in events)
		{
			counter++;

			Logger.LogTrace($"Client: Received event (#{counter}): '{receivedEvent}'.");
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent.StatusCode);
			var line = Assert.Single(receivedEvent.ResponseLines);
			Assert.Equal(AsyncEventContent, line);

			if (counter == ExpectedEventsNo)
			{
				Assert.Equal(1, client.SubscriberCount);
				break;
			}
		}

		// Verifies that "break" in "await foreach" actually removes the internal Channel<T> (subscription).
		Assert.Equal(0, client.SubscriberCount);

		Logger.LogTrace("Client: Done.");
	}

	/// <summary>
	/// Verifies a correct result of a mix of sync and async messages from Tor.
	/// </summary>
	[Fact(Timeout = 120000)] // 2 minutes
	public async Task ReceivingMixOfSyncAndAsyncMessageFromTorControlAsync()
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		// Test parameters.
		const string AsyncEventContent = "CIRC 1000 EXTENDED moria1,moria2";

		var toServer = new Pipe();
		var toClient = new Pipe();

		// Set up Tor control client.
		await using var client = new TorControlClient(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		// Subscribe to Tor events.
		var events = client.ReadEventsAsync(timeoutCts.Token);
		var eventsEnumerator = events.GetAsyncEnumerator();
		var firstReplyTask = eventsEnumerator.MoveNextAsync();

		var serverTask = Task.Run(async () =>
		{
			Logger.LogTrace($"Server: Send msg #1 (async) to client: '650 {AsyncEventContent}'.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);

			Logger.LogTrace($"Server: Send msg #2 (async) to client: '650 {AsyncEventContent}'.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);

			Logger.LogTrace("Server: Wait for TAKEOWNERSHIP command.");
			var command = await toServer.Reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
			Assert.Equal("TAKEOWNERSHIP", command);

			Logger.LogTrace("Server: Send msg #3 (sync) to client in response to TAKEOWNERSHIP command.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"250 OK\r\n", timeoutCts.Token).ConfigureAwait(false);

			Logger.LogTrace($"Server: Send msg #4 (async) to client: '650 {AsyncEventContent}'.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);
		});

		Logger.LogTrace("Client: Receive msg #1 (async).");
		{
			await firstReplyTask.AsTask().WaitAsync(timeoutCts.Token);
			var receivedEvent1 = eventsEnumerator.Current;
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent1.StatusCode);
			var line = Assert.Single(receivedEvent1.ResponseLines);
			Assert.Equal(AsyncEventContent, line);
		}

		// Msg #2 is sent before expected reply (msg #3).
		var takeOwnershipReply = await client.TakeOwnershipAsync(timeoutCts.Token);
		Assert.True(takeOwnershipReply.Success);

		Logger.LogTrace("Client: Receive msg #2 (async).");
		{
			Assert.True(await eventsEnumerator.MoveNextAsync());
			var receivedEvent2 = eventsEnumerator.Current;
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent2.StatusCode);
			var line = Assert.Single(receivedEvent2.ResponseLines);
			Assert.Equal(AsyncEventContent, line);
		}

		Logger.LogTrace("Client: Receive msg #4 (async) - i.e. third async event.");
		{
			Assert.True(await eventsEnumerator.MoveNextAsync());
			var receivedEvent3 = eventsEnumerator.Current;
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent3.StatusCode);
			var line = Assert.Single(receivedEvent3.ResponseLines);
			Assert.Equal(AsyncEventContent, line);
		}

		// Client decided to stop reading Tor async events.
		timeoutCts.Cancel();

		// No more async events.
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await eventsEnumerator.MoveNextAsync());
	}

	/// <summary>
	/// Verifies behavior of the subscription API and its logical subscription model.
	/// </summary>
	[Fact(Timeout = 120000)] // 2 minutes
	public async Task SubscribeAndUnsubscribeAsync()
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		var toServer = new Pipe();
		var toClient = new Pipe();

		// Set up Tor control client.
		await using var client = new TorControlClient(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		Logger.LogTrace("Client: Subscribe 'CIRC' events.");
		{
			var task = client.SubscribeEventsAsync(new string[] { "CIRC" }, timeoutCts.Token);

			Logger.LogTrace("Server: Wait for 'SETEVENTS CIRC' command.");
			var command = await toServer.Reader.ReadLineAsync(timeoutCts.Token);
			Assert.Equal("SETEVENTS CIRC", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}

		Logger.LogTrace("Client: Subscribe 'CIRC' (already subscribed) and 'STATUS_CLIENT' (not subscribed) events.");
		{
			var task = client.SubscribeEventsAsync(new string[] { "CIRC", "STATUS_CLIENT" }, timeoutCts.Token);

			// CIRC is already subscribed.
			Logger.LogTrace("Server: Wait for 'SETEVENTS CIRC STATUS_CLIENT' command.");
			var command = await toServer.Reader.ReadLineAsync(timeoutCts.Token);

			// This means that BOTH 'CIRC' and 'STATUS_CLIENT' must be subscribed now.
			// Note: Given we count logical event subscriptions, 'CIRC' is now (logically) subscribed twice!
			Assert.Equal("SETEVENTS CIRC STATUS_CLIENT", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}

		Logger.LogTrace("Client: Unsubscribe 'CIRC' and 'STATUS_CLIENT' events.");
		{
			var task = client.UnsubscribeEventsAsync(new string[] { "CIRC", "STATUS_CLIENT" }, timeoutCts.Token);

			// CIRC is already subscribed.
			Logger.LogTrace("Server: Wait for 'SETEVENTS CIRC' command.");
			var command = await toServer.Reader.ReadLineAsync(timeoutCts.Token);

			// This means that CIRC is still subscribed (!). The reason for that is that we count logical subscriptions,
			// so when two distinct components can work the subscription API and they don't affect each other.
			Assert.Equal("SETEVENTS CIRC", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}

		Logger.LogTrace("Client: Unsubscribe 'CIRC' events.");
		{
			var task = client.UnsubscribeEventsAsync(new string[] { "CIRC" }, timeoutCts.Token);

			// CIRC is already subscribed.
			Logger.LogTrace("Server: Wait for 'SETEVENTS' command.");
			var command = await toServer.Reader.ReadLineAsync(timeoutCts.Token);

			// This means that no events are subscribed.
			Assert.Equal("SETEVENTS", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}
	}
}
