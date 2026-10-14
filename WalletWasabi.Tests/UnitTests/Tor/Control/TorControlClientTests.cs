using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Messages;
using Xunit;
using static WalletWasabi.Tor.Control.PipeReaderLineReaderExtension;

namespace WalletWasabi.Tests.UnitTests.Tor.Control;

public class TorControlClientTests
{
	/// <summary>Verifies that client receives correct async events from Tor.</summary>
	[Fact]
	public async Task ReceiveTorAsyncEventsUsingForeachAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));

		// Test parameters.
		const int ExpectedEventsNo = 3;
		const string AsyncEventContent = "CIRC 1000 EXTENDED moria1,moria2";

		Pipe toServer = new();
		Pipe toClient = new();

		// Set up Tor control client.
		await using TorControlClient client = new(TorBackend.CTor, pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		// Subscribe to Tor events.
		IAsyncEnumerable<ITorControlReply> events = client.ReadEventsAsync(timeoutCts.Token);

		// Send a Tor event to all subscribed clients (only one here).
		// This must happen after a client is subscribed.
		Task serverTask = Task.Run(async () =>
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
		await foreach (ITorControlReply @event in events)
		{
			counter++;

			Logger.LogTrace($"Client: Received event (#{counter}): '{@event}'.");
			TorControlReply receivedEvent = Assert.IsType<TorControlReply>(@event);

			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent.StatusCode);
			string line = Assert.Single(receivedEvent.ResponseLines);
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

	/// <summary>Verifies a correct result of a mix of sync and async messages from Tor.</summary>
	[Fact]
	public async Task ReceivingMixOfSyncAndAsyncMessageFromTorControlAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(120));

		// Test parameters.
		const string AsyncEventContent = "CIRC 1000 EXTENDED moria1,moria2";

		Pipe toServer = new();
		Pipe toClient = new();

		// Set up Tor control client.
		await using TorControlClient client = new(TorBackend.CTor, pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		// Subscribe to Tor events.
		IAsyncEnumerable<ITorControlReply> events = client.ReadEventsAsync(timeoutCts.Token);
		IAsyncEnumerator<ITorControlReply> eventsEnumerator = events.GetAsyncEnumerator();
		ValueTask<bool> firstReplyTask = eventsEnumerator.MoveNextAsync();

		Task serverTask = Task.Run(async () =>
		{
			Logger.LogTrace($"Server: Send msg #1 (async) to client: '650 {AsyncEventContent}'.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);

			Logger.LogTrace($"Server: Send msg #2 (async) to client: '650 {AsyncEventContent}'.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);

			Logger.LogTrace("Server: Wait for TAKEOWNERSHIP command.");
			string command = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token).ConfigureAwait(false);
			Assert.Equal("TAKEOWNERSHIP", command);

			Logger.LogTrace("Server: Send msg #3 (sync) to client in response to TAKEOWNERSHIP command.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"250 OK\r\n", timeoutCts.Token).ConfigureAwait(false);

			Logger.LogTrace($"Server: Send msg #4 (async) to client: '650 {AsyncEventContent}'.");
			await toClient.Writer.WriteAsciiAndFlushAsync($"650 {AsyncEventContent}\r\n", timeoutCts.Token).ConfigureAwait(false);
		});

		Logger.LogTrace("Client: Receive msg #1 (async).");
		{
			await firstReplyTask.AsTask().WaitAsync(timeoutCts.Token);
			TorControlReply receivedEvent1 = Assert.IsType<TorControlReply>(eventsEnumerator.Current);
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent1.StatusCode);
			string line = Assert.Single(receivedEvent1.ResponseLines);
			Assert.Equal(AsyncEventContent, line);
		}

		// Msg #2 is sent before expected reply (msg #3).
		TorControlReply takeOwnershipReply = await client.TakeOwnershipAsync(timeoutCts.Token);
		Assert.True(takeOwnershipReply.Success);

		Logger.LogTrace("Client: Receive msg #2 (async).");
		{
			Assert.True(await eventsEnumerator.MoveNextAsync());
			TorControlReply receivedEvent2 = Assert.IsType<TorControlReply>(eventsEnumerator.Current);
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent2.StatusCode);
			string line = Assert.Single(receivedEvent2.ResponseLines);
			Assert.Equal(AsyncEventContent, line);
		}

		Logger.LogTrace("Client: Receive msg #4 (async) - i.e. third async event.");
		{
			Assert.True(await eventsEnumerator.MoveNextAsync());
			TorControlReply receivedEvent3 = Assert.IsType<TorControlReply>(eventsEnumerator.Current);
			Assert.Equal(StatusCode.AsynchronousEventNotify, receivedEvent3.StatusCode);
			string line = Assert.Single(receivedEvent3.ResponseLines);
			Assert.Equal(AsyncEventContent, line);
		}

		// Client decided to stop reading Tor async events.
		timeoutCts.Cancel();

		// No more async events.
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await eventsEnumerator.MoveNextAsync());
	}

	/// <summary>Verifies behavior of the subscription API and its logical subscription model.</summary>
	[Fact]
	public async Task SubscribeAndUnsubscribeAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(120));

		Pipe toServer = new();
		Pipe toClient = new();

		// Set up Tor control client.
		await using TorControlClient client = new(TorBackend.CTor, pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		Logger.LogTrace("Client: Subscribe 'CIRC' events.");
		{
			Task task = client.SubscribeEventsAsync(new string[] { "CIRC" }, timeoutCts.Token);

			Logger.LogTrace("Server: Wait for 'SETEVENTS CIRC' command.");
			string command = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token);
			Assert.Equal("SETEVENTS CIRC", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}

		Logger.LogTrace("Client: Subscribe 'CIRC' (already subscribed) and 'STATUS_CLIENT' (not subscribed) events.");
		{
			Task task = client.SubscribeEventsAsync(new string[] { "CIRC", "STATUS_CLIENT" }, timeoutCts.Token);

			// CIRC is already subscribed.
			Logger.LogTrace("Server: Wait for 'SETEVENTS CIRC STATUS_CLIENT' command.");
			string command = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token);

			// This means that BOTH 'CIRC' and 'STATUS_CLIENT' must be subscribed now.
			// Note: Given we count logical event subscriptions, 'CIRC' is now (logically) subscribed twice!
			Assert.Equal("SETEVENTS CIRC STATUS_CLIENT", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}

		Logger.LogTrace("Client: Unsubscribe 'CIRC' and 'STATUS_CLIENT' events.");
		{
			Task task = client.UnsubscribeEventsAsync(new string[] { "CIRC", "STATUS_CLIENT" }, timeoutCts.Token);

			// CIRC is already subscribed.
			Logger.LogTrace("Server: Wait for 'SETEVENTS CIRC' command.");
			string command = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token);

			// This means that CIRC is still subscribed (!). The reason for that is that we count logical subscriptions,
			// so when two distinct components can work the subscription API and they don't affect each other.
			Assert.Equal("SETEVENTS CIRC", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}

		Logger.LogTrace("Client: Unsubscribe 'CIRC' events.");
		{
			Task task = client.UnsubscribeEventsAsync(new string[] { "CIRC" }, timeoutCts.Token);

			// CIRC is already subscribed.
			Logger.LogTrace("Server: Wait for 'SETEVENTS' command.");
			string command = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token);

			// This means that no events are subscribed.
			Assert.Equal("SETEVENTS", command);

			Logger.LogTrace("Server: Reply with OK code.");
			await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

			await task;
		}
	}
}
