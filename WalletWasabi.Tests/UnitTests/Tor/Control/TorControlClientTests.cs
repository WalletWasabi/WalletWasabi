using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using Xunit;
using System.Collections.Generic;

namespace WalletWasabi.Tests.UnitTests.Tor.Control;

/// <summary>
/// Tests for <see cref="TorControlClient"/>.
/// </summary>
public class TorControlClientTests
{
	/// <summary>
	/// Verifies that client receives correct async events from Tor using <see cref="TorControlClient.Subscription.ReadAllAsync(CancellationToken)"/>.
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

		// Set up Tor server task that sends async events to the client.
		var serverTask = Task.Run(async () =>
		{
			// The client registers its subscription before it sends SETEVENTS, so once the command is answered no event can be missed.
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS CIRC", timeoutCts.Token).ConfigureAwait(false);

			for (int i = 0; i < ExpectedEventsNo; i++)
			{
				Logger.LogTrace($"Server: Send async Tor event (#{i}): '650 {AsyncEventContent}'.");
				await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);
			}

			// Disposing the subscription makes the client unsubscribe.
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS", timeoutCts.Token).ConfigureAwait(false);
		});

		Assert.Equal(0, client.SubscriberCount);

		// Subscribe to Tor events.
		var subscription = await client.SubscribeEventsAsync(["CIRC"], timeoutCts.Token);

		// The client should have one subscriber now.
		Assert.Equal(1, client.SubscriberCount);

		int counter = 0;

		// Client should get all the events.
		await foreach (var receivedEvent in subscription.ReadAllAsync(timeoutCts.Token))
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

		// Dispose the subscription and verify that the client has no subscribers.
		Assert.Equal(1, client.SubscriberCount);
		await subscription.DisposeAsync();
		Assert.Equal(0, client.SubscriberCount);

		// Make sure that the server task is completed.
		await serverTask;

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

		var serverTask = Task.Run(async () =>
		{
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS CIRC", timeoutCts.Token).ConfigureAwait(false);

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

			// Disposing the subscription at the end of the test makes the client unsubscribe.
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS", timeoutCts.Token).ConfigureAwait(false);
		});

		// Subscribe to Tor events.
		var subscription = await client.SubscribeEventsAsync(["CIRC"], timeoutCts.Token);

		// Cancellation token source to stop the reader event reading.
		using CancellationTokenSource readEventsByClientCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

		var events = subscription.ReadAllAsync(readEventsByClientCts.Token);
		var eventsEnumerator = events.GetAsyncEnumerator();
		var firstReplyTask = eventsEnumerator.MoveNextAsync();

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
		readEventsByClientCts.Cancel();

		// No more async events.
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await eventsEnumerator.MoveNextAsync());

		await subscription.DisposeAsync();
		await serverTask;
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

		TorControlClient.Subscription subscription1;

		// First logical subscription is set up: Subscribe 'CIRC' events.
		{
			var subscribeTask1 = client.SubscribeEventsAsync(["CIRC"], timeoutCts.Token);
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS CIRC", timeoutCts.Token);
			subscription1 = await subscribeTask1;
			Assert.Equal(1, client.SubscriberCount);
		}

		// Second logical subscription is set up: 'CIRC' (already subscribed) and 'STATUS_CLIENT' (not subscribed)
		{
			var subscribeTask2 = client.SubscribeEventsAsync(["CIRC", "STATUS_CLIENT"], timeoutCts.Token);

			// This means that BOTH 'CIRC' and 'STATUS_CLIENT' must be subscribed now.
			// Note: Given we count logical event subscriptions, 'CIRC' is now (logically) subscribed twice!
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS CIRC STATUS_CLIENT", timeoutCts.Token);
			var subscription2 = await subscribeTask2;
			Assert.Equal(2, client.SubscriberCount);

			var disposeTask2 = subscription2.DisposeAsync();

			// This means that CIRC is still subscribed (!). The reason for that is that we count logical subscriptions,
			// so when two distinct components use the subscription API they don't affect each other.
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS CIRC", timeoutCts.Token);
			await disposeTask2;
			Assert.Equal(1, client.SubscriberCount);
		}

		// Unsubscribe the first logical subscription. This means that no events are subscribed anymore.
		{
			var disposeTask1 = subscription1.DisposeAsync();

			// This means that no events are subscribed.
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS", timeoutCts.Token);
			await disposeTask1;
			Assert.Equal(0, client.SubscriberCount);
		}

		// Dispose the first subscription again. This should be a no-op and not throw an exception.
		await subscription1.DisposeAsync();
	}

	/// <summary>
	/// Tests that we subscribe to a Tor control server and then the connection is lost. The subscription must stop providing events.
	/// A new subscription must announce that there will be no events right away.
	/// </summary>
	/// <returns></returns>
	[Fact(Timeout = 120000)] // 2 minutes
	public async Task SubscriptionEvents_ConnectionToTorIsLostAsync()
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		var toServer = new Pipe();
		var toClient = new Pipe();

		await using var client = new TorControlClient(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		TorControlClient.Subscription subscription1;

		// First subscription is set up and then the Tor control connection is lost.
		{
			var subscribeTask = client.SubscribeEventsAsync(["CIRC"], timeoutCts.Token);
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS CIRC", timeoutCts.Token);
			subscription1 = await subscribeTask;

			// Tor goes away.
			await toClient.Writer.CompleteAsync();

			// The existing subscription reports "no more events".
			await foreach (var _ in subscription1.ReadAllAsync(timeoutCts.Token))
			{
				Assert.Fail("No event was expected.");
			}
		}

		TorControlClient.Subscription subscription2;

		// The second subscription is set up after the Tor control connection is lost. No events are expected, but the subscription succeeds.
		{
			// 'CIRC' is already subscribed, so no SETEVENTS is sent and the call succeeds.
			subscription2 = await client.SubscribeEventsAsync(["CIRC"], timeoutCts.Token);

			// The new subscription must report "no more events" right away.
			await foreach (var _ in subscription2.ReadAllAsync(timeoutCts.Token))
			{
				Assert.Fail("No event was expected.");
			}

			// Subscribers stay registered until subscriptions are disposed.
			Assert.Equal(2, client.SubscriberCount);
		}

		await subscription1.DisposeAsync();
		await subscription2.DisposeAsync();

		Assert.Equal(0, client.SubscriberCount);
	}

	[Fact(Timeout = 120000)]
	public async Task SubscriptionEvents_FailureLeadsToRollbackAsync()
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		var toServer = new Pipe();
		var toClient = new Pipe();

		await using var client = new TorControlClient(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		// The first subscription fails because Tor doesn't recognize the event name.
		{
			var task = client.SubscribeEventsAsync(["BOGUS"], timeoutCts.Token);
			Assert.Equal("SETEVENTS BOGUS", await toServer.Reader.ReadLineAsync(timeoutCts.Token));
			await toClient.Writer.WriteAsciiAndFlushAsync("552 Unrecognized event \"BOGUS\"\r\n", timeoutCts.Token);

			await Assert.ThrowsAsync<TorControlException>(async () => await task);
			Assert.Equal(0, client.SubscriberCount);
		}

		// The second subscription of the same event succeeds (for the sake of the test).
		{
			var retry = client.SubscribeEventsAsync(["BOGUS"], timeoutCts.Token);
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS BOGUS", timeoutCts.Token);
			var subscription = await retry;

			Assert.Equal(1, client.SubscriberCount);

			var dispose = subscription.DisposeAsync();
			await HandleSetEventsRequestByServerAsync(toServer, toClient, "SETEVENTS", timeoutCts.Token);
			await dispose;

			Assert.Equal(0, client.SubscriberCount);
		}
	}

	/// <summary>
	/// Verifies that if <see cref="TorControlClient.SubscribeEventsAsync(IReadOnlyList{string}, CancellationToken)"/> is canceled,
	/// then the internal message loop IS *corrupted*.
	/// <para>This tests demonstrates a bug that is documented and accepted as a limitation of the current implementation.</para>
	/// </summary>
	[Fact(Timeout = 120000)] // 2 minutes
	public async Task SubscriptionEvents_SubscriptionCancellingCorruptsStateAsync()
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		using var subscribeCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

		var toServer = new Pipe();
		var toClient = new Pipe();

		await using var client = new TorControlClient(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		// The command reaches the server, but the server does not answer yet.
		var subscribeTask = client.SubscribeEventsAsync(["CIRC"], subscribeCts.Token);
		Assert.Equal("SETEVENTS CIRC", await toServer.Reader.ReadLineAsync(timeoutCts.Token));

		subscribeCts.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await subscribeTask);
		Assert.Equal(0, client.SubscriberCount);

		// Tor answers the cancelled command late.
		await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

		// A different command must get ITS OWN reply, not the stale "250 OK".
		var getConfTask = client.GetConfAsync("SocksPort", timeoutCts.Token);
		Assert.Equal("GETCONF SocksPort", await toServer.Reader.ReadLineAsync(timeoutCts.Token));
		await toClient.Writer.WriteAsciiAndFlushAsync("250 SocksPort=9050\r\n", timeoutCts.Token);

		var reply = await getConfTask;

		// The expected reply is "250 SocksPort=9050", not the stale "250 OK" from the cancelled command!
		// This is a known issue with the current implementation.
		Assert.Equal("OK", Assert.Single(reply.ResponseLines));
		Assert.NotEqual("SocksPort=9050", Assert.Single(reply.ResponseLines));
	}

	/// <summary>
	/// Simulates Tor's side of a <c>SETEVENTS</c> exchange: expects the command and answers it with <c>250 OK</c>.
	/// </summary>
	private static async Task HandleSetEventsRequestByServerAsync(Pipe toServer, Pipe toClient, string expectedCommand, CancellationToken cancellationToken)
	{
		Logger.LogTrace($"Server: Wait for '{expectedCommand}' command.");
		var command = await toServer.Reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
		Assert.Equal(expectedCommand, command);

		Logger.LogTrace("Server: Reply with OK code.");
		await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", cancellationToken).ConfigureAwait(false);
	}
}
