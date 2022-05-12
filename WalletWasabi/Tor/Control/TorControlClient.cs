using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control;

/// <summary>Client already authenticated to Tor Control.</summary>
public class TorControlClient : IAsyncDisposable
{
	private static readonly UnboundedChannelOptions Options = new()
	{
		SingleWriter = true
	};

	/// <remarks>This helps with graceful stopping of the reader loop.</remarks>
	private volatile bool _readLastSyncReply;

	public TorControlClient(TcpClient tcpClient) :
		this(PipeReader.Create(tcpClient.GetStream()), PipeWriter.Create(tcpClient.GetStream()))
	{
		TcpClient = tcpClient;
	}

	internal TorControlClient(PipeReader pipeReader, PipeWriter pipeWriter)
	{
		TcpClient = null;
		PipeReader = pipeReader;
		PipeWriter = pipeWriter;
		ReaderCts = new();

		SyncChannel = Channel.CreateUnbounded<TorControlReply>(Options);
		AsyncChannels = new List<Channel<TorControlReply>>();
		ReaderLoopTask = Task.Run(ReaderLoopAsync);
		MessageLock = new();
	}

	private TcpClient? TcpClient { get; }
	private PipeReader PipeReader { get; }
	private PipeWriter PipeWriter { get; }
	private CancellationTokenSource ReaderCts { get; }
	private Task ReaderLoopTask { get; }

	/// <summary>Channel only for synchronous replies from Tor control.</summary>
	/// <remarks>Typically, there is at most one message in the channel at a time.</remarks>
	private Channel<TorControlReply> SyncChannel { get; }

	/// <summary>Guards <see cref="AsyncChannels"/>.</summary>
	private object AsyncChannelsLock { get; } = new();

	/// <summary>Channel only for <see cref="StatusCode.AsynchronousEventNotify"/> events.</summary>
	/// <remarks>
	/// Guarded by <see cref="AsyncChannelsLock"/>.
	/// <para>This list should be used only in a copy-on-write way to avoid iterating a modified list.</para>
	/// </remarks>
	private List<Channel<TorControlReply>> AsyncChannels { get; set; }

	/// <summary>Lock to when sending a request to Tor control and waiting for a reply.</summary>
	/// <remarks>Tor control protocol does not provide a foolproof way to recognize that a response belongs to a request.</remarks>
	private AsyncLock MessageLock { get; }

	/// <summary>Key represents an event name and value represents a subscription counter.</summary>
	private SortedDictionary<string, int> SubscribedEvents { get; } = new();

	/// <summary>Lock to guard all access to <see cref="SubscribedEvents"/>.</summary>
	/// <remarks><see cref="MessageLock"/> must be locked first if it is needed too.</remarks>
	private object SubscriptionEventsLock { get; } = new();

	/// <summary>Number of subscribers that currently listen to Tor's async events using <see cref="ReadEventsAsync"/>.</summary>
	/// <remarks>Mainly for tests.</remarks>
	internal int SubscriberCount
	{
		get
		{
			lock (AsyncChannelsLock)
			{
				return AsyncChannels.Count;
			}
		}
	}

	/// <summary>
	/// Gets protocol info (for version 1).
	/// </summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">3.21. PROTOCOLINFO</seealso>
	public async Task<ProtocolInfoReply> GetProtocolInfoAsync(CancellationToken cancellationToken = default)
	{
		// Grammar: "PROTOCOLINFO" *(SP PIVERSION) CRLF
		// Note: PIVERSION is there in case we drastically change the syntax one day. For now it should always be "1".
		TorControlReply reply = await SendCommandAsync("PROTOCOLINFO 1\r\n", cancellationToken).ConfigureAwait(false);

		return ProtocolInfoReply.FromReply(reply);
	}

	/// <summary>
	/// Gets process ID belonging to the main Tor process.
	/// </summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">3.9. GETINFO</seealso>
	public async Task<int> GetTorProcessIdAsync(CancellationToken cancellationToken = default)
	{
		TorControlReply reply = await SendCommandAsync("GETINFO process/pid\r\n", cancellationToken).ConfigureAwait(false);

		if (!reply.Success)
		{
			throw new TorControlException("Failed to get Tor process ID."); // This should never happen.
		}

		(string key, string value, string _) = Tokenizer.ReadKeyValueAssignment(reply.ResponseLines[0]);

		if (key != "process/pid")
		{
			throw new TorControlException("Invalid key received."); // This should never happen.
		}

		int result = int.Parse(value);

		return result;
	}

	/// <summary>
	/// Gets Tor's circuits that are currently available.
	/// </summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">3.9. GETINFO, and 4.1.1. Circuit status changed.</seealso>
	public async Task<GetInfoCircuitStatusReply> GetCircuitStatusAsync(CancellationToken cancellationToken = default)
	{
		TorControlReply reply = await SendCommandAsync("GETINFO circuit-status\r\n", cancellationToken).ConfigureAwait(false);

		return GetInfoCircuitStatusReply.FromReply(reply);
	}

	/// <summary>
	/// Instructs Tor to shut down when this control connection is closed.
	/// </summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.23. TAKEOWNERSHIP.</seealso>
	public Task<TorControlReply> TakeOwnershipAsync(CancellationToken cancellationToken = default)
		=> SendCommandAsync("TAKEOWNERSHIP\r\n", cancellationToken);

	/// <summary>
	/// Requests controlled shutdown.
	/// </summary>
	/// <remarks>If server is a client (OP), exit immediately. If it's a relay (OR), close listeners and exit after <c>ShutdownWaitLength</c> seconds.</remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.7. SIGNAL.</seealso>
	public async Task<TorControlReply> SignalShutdownAsync(CancellationToken cancellationToken = default)
	{
		using IDisposable _ = await MessageLock.LockAsync(cancellationToken).ConfigureAwait(false);

		// This assignment must be done after MessageLock is acquired to prevent the race in calling SendCommand method by someone else.
		_readLastSyncReply = true;

		TorControlReply reply = await SendCommandNoLockAsync("SIGNAL SHUTDOWN\r\n", cancellationToken).ConfigureAwait(false);

		if (reply.Success)
		{
			ReaderCts.Cancel();
		}

		return reply;
	}

	/// <summary>
	/// Causes Tor to stop polling for the existence of a process with its owning controller's PID.
	/// </summary>
	/// <remarks>If TAKEOWNERSHIP command is sent, Tor will still exit when the control connection ends.</remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.2. RESETCONF and 3.23. TAKEOWNERSHIP.</seealso>
	public Task<TorControlReply> ResetOwningControllerProcessConfAsync(CancellationToken cancellationToken = default)
		=> SendCommandAsync("RESETCONF __OwningControllerProcess\r\n", cancellationToken);

	/// <summary>Sends a command to Tor.</summary>
	/// <remarks>This is meant as a low-level API method, if needed for some reason.</remarks>
	/// <param name="command">A Tor control command which must end with <c>\r\n</c>.</param>
	public async Task<TorControlReply> SendCommandAsync(string command, CancellationToken cancellationToken = default)
	{
		using IDisposable _ = await MessageLock.LockAsync(cancellationToken).ConfigureAwait(false);
		return await SendCommandNoLockAsync(command, cancellationToken).ConfigureAwait(false);
	}

	/// <remarks>Lock <see cref="MessageLock"/> must be acquired by the caller.</remarks>
	private async Task<TorControlReply> SendCommandNoLockAsync(string command, CancellationToken cancellationToken = default)
	{
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ReaderCts.Token, cancellationToken);

		Logger.LogTrace($"Client: About to send command: '{command.TrimEnd()}'");
		await PipeWriter.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(command)), linkedCts.Token).ConfigureAwait(false);
		await PipeWriter.FlushAsync(linkedCts.Token).ConfigureAwait(false);

		TorControlReply reply = await SyncChannel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
		Logger.LogTrace($"Client: Reply: '{reply}'");

		return reply;
	}

	/// <summary>Allows the caller to read Tor events using <c>await foreach</c>.</summary>
	/// <remarks>Processing of replies does not block other readers.</remarks>
	/// <example>
	/// <code>
	/// await foreach (TorControlReply reply in TorControlClient.ReadEventsAsync())
	/// {
	///    Console.WriteLine(reply.ToString());
	/// }
	/// </code> 
	/// </example>
	public async IAsyncEnumerable<TorControlReply> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Channel<TorControlReply> channel = Channel.CreateUnbounded<TorControlReply>(Options);
		List<Channel<TorControlReply>> newList;

		try
		{
			lock (AsyncChannelsLock)
			{
				newList = new(AsyncChannels)
				{
					channel
				};

				AsyncChannels = newList;
			}

			Logger.LogTrace($"ReadEventsAsync: subscribers: {newList.Count}.");
			await foreach (TorControlReply item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return item;
			}
		}
		finally
		{
			Logger.LogTrace("ReadEventsAsync: About to unsubscribe.");
			lock (AsyncChannelsLock)
			{
				newList = new(AsyncChannels);
				_ = newList.Remove(channel);

				AsyncChannels = newList;
			}
		}
	}

	/// <returns>List of event names like <c>CIRC</c>, <c>STATUS_CLIENT</c>, etc.</returns>
	public List<string> GetSubscribedEvents()
	{
		lock (SubscriptionEventsLock)
		{
			// Return a copy to avoid multi-threading issues.
			return SubscribedEvents.Keys.ToList();
		}
	}

	/// <summary>Subscribes Tor control events by their names.</summary>
	/// <remarks>If an event stream is already subscribed, no command is sent to Tor control.</remarks>
	/// <param name="cancellationToken">
	/// Useful when the whole Tor process stops. Otherwise, the internal state of this object may get corrupted.
	/// </param>
	public async Task SubscribeEventsAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
	{
		using IDisposable _ = await MessageLock.LockAsync(cancellationToken).ConfigureAwait(false);

		bool sendCommand = false;
		string subscribedEventNames;

		lock (SubscriptionEventsLock)
		{
			foreach (string eventName in names)
			{
				if (SubscribedEvents.TryGetValue(eventName, out int counter))
				{
					SubscribedEvents[eventName] = counter + 1;
				}
				else
				{
					sendCommand = true;
					SubscribedEvents.Add(eventName, 1);
				}
			}

			// Get all event names that must be subscribed.
			subscribedEventNames = string.Join(' ', SubscribedEvents.Keys);
		}

		if (sendCommand)
		{
			TorControlReply reply = await SendCommandNoLockAsync($"SETEVENTS {subscribedEventNames}\r\n", cancellationToken).ConfigureAwait(false);

			if (!reply.Success)
			{
				// This should never happen.
				throw new TorControlException("Failed to subscribe events.");
			}
		}
	}

	/// <summary>Unsubscribes Tor control events by their names.</summary>
	/// <remarks>If the event listener counter gets to zero, the event stream is actually truly unsubscribed.</remarks>
	/// <param name="cancellationToken">
	/// Useful when the whole application stops. Otherwise, the internal state of this object may get corrupted.
	/// </param>
	public async Task UnsubscribeEventsAsync(string[] names, CancellationToken cancellationToken = default)
	{
		using IDisposable _ = await MessageLock.LockAsync(cancellationToken).ConfigureAwait(false);

		bool sendCommand = false;
		string subscribedEventNames;

		lock (SubscriptionEventsLock)
		{
			foreach (string eventName in names)
			{
				if (SubscribedEvents.TryGetValue(eventName, out int counter))
				{
					counter--;

					if (counter > 0)
					{
						SubscribedEvents[eventName] = counter;
					}
					else
					{
						SubscribedEvents.Remove(eventName);
						sendCommand = true;
					}
				}
			}

			// Get all event names that remained.
			subscribedEventNames = string.Join(' ', SubscribedEvents.Keys);
		}

		if (sendCommand)
		{
			string command = subscribedEventNames == "" ? "SETEVENTS\r\n" : $"SETEVENTS {subscribedEventNames}\r\n";
			TorControlReply reply = await SendCommandNoLockAsync(command, cancellationToken).ConfigureAwait(false);

			if (!reply.Success)
			{
				// This should never happen.
				throw new TorControlException("Failed to unsubscribe events.");
			}
		}
	}

	/// <summary>Unsubscribes all Tor control events.</summary>
	public async Task<bool> UnsubscribeAllEventsAsync()
	{
		// Sanity timeout.
		using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
		using IDisposable _ = await MessageLock.LockAsync(cts.Token).ConfigureAwait(false);

		int count;

		lock (SubscriptionEventsLock)
		{
			count = SubscribedEvents.Keys.Count;
			SubscribedEvents.Clear();
		}

		if (count > 0)
		{
			TorControlReply reply = await SendCommandNoLockAsync($"SETEVENTS\r\n", cts.Token).ConfigureAwait(false);

			if (!reply.Success)
			{
				// This should never happen.
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Loop that continues reading received messages from Tor control TCP connection.
	/// </summary>
	private async Task ReaderLoopAsync()
	{
		try
		{
			while (!ReaderCts.IsCancellationRequested)
			{
				TorControlReply reply = await TorControlReplyReader.ReadReplyAsync(PipeReader, ReaderCts.Token).ConfigureAwait(false);

				if (reply.StatusCode == StatusCode.AsynchronousEventNotify)
				{
					List<Channel<TorControlReply>> list;

					lock (AsyncChannelsLock)
					{
						list = AsyncChannels;
					}

					// Notify every "subscriber" who reads all Tor events.
					foreach (Channel<TorControlReply> channel in list)
					{
						await channel.Writer.WriteAsync(reply, ReaderCts.Token).ConfigureAwait(false);
					}
				}
				else
				{
					// Propagate a response back to the requester.
					await SyncChannel.Writer.WriteAsync(reply, ReaderCts.Token).ConfigureAwait(false);

					if (_readLastSyncReply)
					{
						Logger.LogTrace("Request to read last message was issued. No more message reading.");
						break;
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace("Reader loop was stopped.");
		}
		catch (IOException e)
		{
			Logger.LogError("Reply reader failed to read from pipe. Internal stream was most likely forcefully closed.", e);
		}
		catch (TorControlReplyParseException e) when (e.Message == "No reply line was received.")
		{
			Logger.LogError("Incomplete Tor control reply was received. Tor probably terminated abruptly.", e);
		}
		catch (Exception e)
		{
			// This is an unrecoverable issue.
			Logger.LogError($"Exception occurred in the reader loop: {e}.");
			throw;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			bool isOk = await UnsubscribeAllEventsAsync().ConfigureAwait(false);

			if (!isOk)
			{
				Logger.LogWarning("Failed to unsubscribe all Tor control events.");
			}
		}
		catch (Exception e)
		{
			Logger.LogDebug("Tor process might have terminated, so we cannot unsubscribe Tor events.");
			Logger.LogTrace(e);
		}

		// Stop reader loop.
		ReaderCts.Cancel();

		try
		{
			// Wait until the reader loop stops.
			await ReaderLoopTask.ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Logger.LogDebug("Tor process might have terminated and so the reading loop might have terminated abruptly.");
			Logger.LogTrace(e);
		}

		ReaderCts.Dispose();
		TcpClient?.Dispose();
	}
}
