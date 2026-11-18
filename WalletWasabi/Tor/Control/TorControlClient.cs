using Nito.AsyncEx;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control;

/// <summary>
/// Client already authenticated to Tor Control.
/// </summary>
public class TorControlClient : IAsyncDisposable
{
	private static readonly UnboundedChannelOptions Options = new()
	{
		SingleWriter = true
	};

	/// <remarks>This helps with graceful stopping of the reader loop.</remarks>
	private volatile bool _readLastSyncReply;

	/// <remarks>Guarded by <see cref="_asyncChannelsLock"/>.</remarks>
	private bool _readerStopped;

	public TorControlClient(TcpClient tcpClient) :
		this(PipeReader.Create(tcpClient.GetStream()), PipeWriter.Create(tcpClient.GetStream()))
	{
		_tcpClient = tcpClient;
	}

	internal TorControlClient(PipeReader pipeReader, PipeWriter pipeWriter)
	{
		_tcpClient = null;
		_pipeReader = pipeReader;
		_pipeWriter = pipeWriter;

		_syncChannel = Channel.CreateUnbounded<TorControlReply>(Options);
		AsyncChannels = [];
		_readerLoopTask = Task.Run(ReaderLoopAsync);
	}

	private readonly TcpClient? _tcpClient;
	private readonly PipeReader _pipeReader;
	private readonly PipeWriter _pipeWriter;
	private readonly CancellationTokenSource _readerCts = new();
	private readonly Task _readerLoopTask;

	/// <summary>Channel only for synchronous replies from Tor control.</summary>
	/// <remarks>Typically, there is at most one message in the channel at a time.</remarks>
	private readonly Channel<TorControlReply> _syncChannel;

	/// <summary>Guards <see cref="AsyncChannels"/>.</summary>
	private readonly Lock _asyncChannelsLock = new();

	/// <summary>Subscribed events. The events can be added but never removed.</summary>
	/// <remarks>Guarded by <see cref="_messageLock"/>.</remarks>
	private SortedSet<string> _subscribedEvents = new(StringComparer.Ordinal);

	/// <summary>Channel only for <see cref="StatusCode.AsynchronousEventNotify"/> events.</summary>
	/// <remarks>
	/// Guarded by <see cref="_asyncChannelsLock"/>.
	/// <para>This list should be used only in a copy-on-write way to avoid iterating a modified list.</para>
	/// </remarks>
	private ImmutableList<Channel<TorControlReply>> AsyncChannels { get; set; }

	/// <summary>Lock to when sending a request to Tor control and waiting for a reply.</summary>
	/// <remarks>Tor control protocol does not provide a foolproof way to recognize that a response belongs to a request.</remarks>
	private readonly AsyncLock _messageLock = new();


	/// <summary>Number of subscribers that currently listen to Tor's async events.</summary>
	/// <remarks>Mainly for tests.</remarks>
	internal int SubscriberCount
	{
		get
		{
			lock (_asyncChannelsLock)
			{
				return AsyncChannels.Count;
			}
		}
	}

	/// <summary>
	/// Gets the value of zero or more configuration variable(s).
	/// </summary>
	/// <remarks>Currently we support only one keyword and not multiple ones.</remarks>
	/// <seealso href="https://gitlab.torproject.org/tpo/core/torspec/-/blob/main/spec/control-spec/commands.md#getconf"/>
	public async Task<TorControlReply> GetConfAsync(string keyword, CancellationToken cancellationToken)
	{
		// Grammar: "GETCONF" *(SP keyword) CRLF
		return await SendCommandAsync($"GETCONF {keyword}\r\n", cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets protocol info (for version 1).
	/// </summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">3.21. PROTOCOLINFO</seealso>
	public async Task<ProtocolInfoReply> GetProtocolInfoAsync(CancellationToken cancellationToken)
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
	public async Task<int> GetTorProcessIdAsync(CancellationToken cancellationToken)
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
	/// Instructs Tor to shut down when this control connection is closed.
	/// </summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.23. TAKEOWNERSHIP.</seealso>
	public Task<TorControlReply> TakeOwnershipAsync(CancellationToken cancellationToken)
		=> SendCommandAsync("TAKEOWNERSHIP\r\n", cancellationToken);

	/// <summary>
	/// Requests controlled shutdown.
	/// </summary>
	/// <remarks>If server is a client (OP), exit immediately. If it's a relay (OR), close listeners and exit after <c>ShutdownWaitLength</c> seconds.</remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.7. SIGNAL.</seealso>
	public async Task<TorControlReply> SignalShutdownAsync(CancellationToken cancellationToken)
	{
		using IDisposable _ = await _messageLock.LockAsync(cancellationToken).ConfigureAwait(false);

		// This assignment must be done after _messageLock is acquired to prevent the race in calling SendCommand method by someone else.
		_readLastSyncReply = true;

		TorControlReply reply = await SendCommandNoLockAsync("SIGNAL SHUTDOWN\r\n", cancellationToken).ConfigureAwait(false);

		if (reply.Success)
		{
			_readerCts.Cancel();
		}

		return reply;
	}

	public Task<string> CreateEphemeralOnionServiceAsync(int virtualPort, int remotePort, CancellationToken cancellationToken)
	{
		return CreateOnionServiceAsync("NEW:BEST", "Flags=DiscardPK", virtualPort, remotePort, cancellationToken);
	}

	public async Task<(string, string)> CreateKeylessOnionServiceAsync(int virtualPort, int remotePort, CancellationToken cancellationToken)
	{
		var reply = await CreateOnionServiceCommandAsync("NEW:ED25519-V3", "", virtualPort, remotePort, cancellationToken).ConfigureAwait(false);

		const string ServiceIdMarker = "ServiceID=";
		var serviceLine = reply.ResponseLines.FirstOrDefault(x => x.StartsWith(ServiceIdMarker, StringComparison.Ordinal)) ??
			throw new TorControlException("Tor protocol violation.");

		const string PrivateKeyMarker = "PrivateKey=";
		var privateKeyLine = reply.ResponseLines.FirstOrDefault(x => x.StartsWith(PrivateKeyMarker, StringComparison.Ordinal)) ??
			throw new TorControlException("Tor protocol violation.");

		var serviceId = serviceLine[ServiceIdMarker.Length..];
		var privateKey = privateKeyLine[PrivateKeyMarker.Length..];
		return (serviceId, privateKey);
	}

	public Task<string> CreateOnionServiceAsync(string key, int virtualPort, int remotePort, CancellationToken cancellationToken)
	{
		return CreateOnionServiceAsync(key, "", virtualPort, remotePort, cancellationToken);
	}

	private async Task<string> CreateOnionServiceAsync(string key, string flags, int virtualPort, int remotePort, CancellationToken cancellationToken)
	{
		var reply = await CreateOnionServiceCommandAsync(key, flags, virtualPort, remotePort, cancellationToken).ConfigureAwait(false);

		const string Marker = "ServiceID=";
		var serviceLine = reply.ResponseLines.FirstOrDefault(x => x.StartsWith(Marker, StringComparison.Ordinal)) ??
			throw new TorControlException("Tor protocol violation.");

		var serviceId = serviceLine[Marker.Length..];
		return serviceId;
	}

	private async Task<TorControlReply> CreateOnionServiceCommandAsync(string key, string flags, int virtualPort,
		int remotePort, CancellationToken cancellationToken)
	{
		var reply = await SendCommandAsync($"ADD_ONION {key} {flags} Port={virtualPort},{remotePort}\r\n", cancellationToken).ConfigureAwait(false);
		if (!reply.Success)
		{
			throw new TorControlException("Failed to create onion service.");
		}

		return reply;
	}

	public async Task<bool> DestroyOnionServiceAsync(string serviceId, CancellationToken cancellationToken)
	{
		var reply = await SendCommandAsync($"DEL_ONION {serviceId}\r\n", cancellationToken).ConfigureAwait(false);
		return reply.Success;
	}

	/// <summary>
	/// Causes Tor to stop polling for the existence of a process with its owning controller's PID.
	/// </summary>
	/// <remarks>If TAKEOWNERSHIP command is sent, Tor will still exit when the control connection ends.</remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.2. RESETCONF and 3.23. TAKEOWNERSHIP.</seealso>
	public Task<TorControlReply> ResetOwningControllerProcessConfAsync(CancellationToken cancellationToken)
		=> SendCommandAsync("RESETCONF __OwningControllerProcess\r\n", cancellationToken);

	/// <summary>
	/// Sends a command to Tor.
	/// </summary>
	/// <remarks>This is meant as a low-level API method, if needed for some reason.</remarks>
	/// <param name="command">A Tor control command which must end with <c>\r\n</c>.</param>
	public async Task<TorControlReply> SendCommandAsync(string command, CancellationToken cancellationToken)
	{
		using IDisposable _ = await _messageLock.LockAsync(cancellationToken).ConfigureAwait(false);
		return await SendCommandNoLockAsync(command, cancellationToken).ConfigureAwait(false);
	}

	/// <remarks>Lock <see cref="_messageLock"/> must be acquired by the caller.</remarks>
	private async Task<TorControlReply> SendCommandNoLockAsync(string command, CancellationToken cancellationToken)
	{
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_readerCts.Token, cancellationToken);

		Logger.LogTrace($"Client: About to send command: '{command.TrimEnd()}'");
		await _pipeWriter.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(command)), linkedCts.Token).ConfigureAwait(false);
		await _pipeWriter.FlushAsync(linkedCts.Token).ConfigureAwait(false);

		var reply = await _syncChannel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
		Logger.LogTrace($"Client: Reply: '{reply}'");

		return reply;
	}

	/// <returns>Number of subscribed readers.</returns>
	private int AddAsyncChannel(Channel<TorControlReply> channel)
	{
		lock (_asyncChannelsLock)
		{
			AsyncChannels = AsyncChannels.Add(channel);

			if (_readerStopped)
			{
				// Nothing will ever be written to this channel, complete the channel right away.
				channel.Writer.TryComplete();
			}

			return AsyncChannels.Count;
		}
	}

	private void RemoveAsyncChannel(Channel<TorControlReply> channel)
	{
		lock (_asyncChannelsLock)
		{
			AsyncChannels = AsyncChannels.Remove(channel);
		}
	}

	/// <summary>
	/// Subscribes Tor control events by their names.
	/// </summary>
	/// <param name="cancellationToken">
	/// DANGER: Cancelling after the command was sent makes all later replies off by one. Pass a token only if the client is going to be discarded.
	/// </param>
	/// <remarks>If an event stream is already subscribed, no command is sent to Tor control.</remarks>
	public async Task<Subscription> SubscribeEventsAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
	{
		using IDisposable _ = await _messageLock.LockAsync(cancellationToken).ConfigureAwait(false);

		var channel = Channel.CreateUnbounded<TorControlReply>(Options);
		AddAsyncChannel(channel);

		try
		{
			var wanted = new SortedSet<string>(_subscribedEvents, StringComparer.Ordinal);
			bool changed = false;

			foreach (string name in names)
			{
				changed |= wanted.Add(name);
			}

			if (changed)
			{
				var reply = await SendCommandNoLockAsync($"SETEVENTS {string.Join(' ', wanted)}\r\n", cancellationToken).ConfigureAwait(false);

				if (!reply.Success)
				{
					throw new TorControlException("Failed to subscribe events.");
				}

				// Intentionally after the command succeeded.
				_subscribedEvents = wanted;
			}

			return new Subscription(this, channel);
		}
		catch
		{
			RemoveAsyncChannel(channel);
			throw;
		}
	}

	/// <summary>
	/// Loop that continues reading received messages from Tor control TCP connection.
	/// </summary>
	private async Task ReaderLoopAsync()
	{
		Exception? exception = null;

		try
		{
			while (!_readerCts.IsCancellationRequested)
			{
				TorControlReply reply = await TorControlReplyReader.ReadReplyAsync(_pipeReader, _readerCts.Token).ConfigureAwait(false);

				if (reply.StatusCode == StatusCode.AsynchronousEventNotify)
				{
					ImmutableList<Channel<TorControlReply>> list;

					lock (_asyncChannelsLock)
					{
						list = AsyncChannels;
					}

					// Notify every "subscriber" who reads all Tor events.
					foreach (var channel in list)
					{
						channel.Writer.TryWrite(reply);
					}
				}
				else
				{
					// Propagate a response back to the requester.
					await _syncChannel.Writer.WriteAsync(reply, _readerCts.Token).ConfigureAwait(false);

					if (_readLastSyncReply)
					{
						Logger.LogTrace("Request to read last message was issued. No more message reading.");
						break;
					}
				}
			}
		}
		catch (OperationCanceledException e)
		{
			Logger.LogTrace("Reader loop was stopped.");
			exception = e;
		}
		catch (IOException e)
		{
			Logger.LogError("Reply reader failed to read from pipe. Internal stream was most likely forcefully closed.", e);
			exception = e;
		}
		catch (TorControlReplyParseException e) when (e.Message == "No reply line was received.")
		{
			Logger.LogError("Incomplete Tor control reply was received. Tor probably terminated abruptly.", e);
			exception = e;
		}
		catch (Exception e)
		{
			// This is an unrecoverable issue.
			Logger.LogError($"Exception occurred in the reader loop: {e}.");
			exception = e;
			throw;
		}
		finally
		{
			_syncChannel.Writer.Complete(exception);

			ImmutableList<Channel<TorControlReply>> subscribers;

			lock (_asyncChannelsLock)
			{
				_readerStopped = true;
				subscribers = AsyncChannels;
			}

			foreach (var subscriber in subscribers)
			{
				subscriber.Writer.TryComplete();
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		// Stop reader loop.
		_readerCts.Cancel();

		try
		{
			// Wait until the reader loop stops.
			await _readerLoopTask.ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Logger.LogDebug("Tor process might have terminated and so the reading loop might have terminated abruptly.");
			Logger.LogTrace(e);
		}

		_readerCts.Dispose();
		_tcpClient?.Dispose();
	}

	/// <summary>
	/// Active subscription to Tor control events.
	/// </summary>
	/// <remarks>
	/// The channel receives *all* asynchronous events, not only those whose names were subscribed.
	/// <para>Disposing does NOT unsubscribe from Tor events.</para>
	/// </remarks>
	public sealed class Subscription : IDisposable
	{
		private readonly TorControlClient _client;
		private readonly Channel<TorControlReply> _channel;
		private int _disposed;

		internal Subscription(TorControlClient client, Channel<TorControlReply> channel)
		{
			_client = client;
			_channel = channel;
		}

		/// <summary>
		/// Reads *all* events (not just subscribed ones!) until the subscription is disposed or the Tor control connection is closed.
		/// </summary>
		public IAsyncEnumerable<TorControlReply> ReadAllAsync(CancellationToken cancellationToken)
			=> _channel.Reader.ReadAllAsync(cancellationToken);

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
			{
				return;
			}

			_client.RemoveAsyncChannel(_channel);
			_channel.Writer.TryComplete();
		}
	}
}
