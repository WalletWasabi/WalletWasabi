using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control
{
	/// <summary>
	/// Client already authenticated to Tor Control.
	/// </summary>
	public class TorControlClient : IAsyncDisposable
	{
		private static readonly UnboundedChannelOptions Options = new()
		{
			SingleWriter = true
		};

		public TorControlClient(TcpClient tcpClient) :
			this(PipeReader.Create(tcpClient.GetStream()), PipeWriter.Create(tcpClient.GetStream()))
		{
			TcpClient = tcpClient;
		}

		/// <summary>For testing.</summary>
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
		public Task<TorControlReply> SignalShutdownAsync(CancellationToken cancellationToken = default)
			=> SendCommandAsync("SIGNAL SHUTDOWN\r\n", cancellationToken);

		/// <summary>
		/// Causes Tor to stop polling for the existence of a process with its owning controller's PID.
		/// </summary>
		/// <remarks>If TAKEOWNERSHIP command is sent, Tor will still exit when the control connection ends.</remarks>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.2. RESETCONF and 3.23. TAKEOWNERSHIP.</seealso>
		public Task<TorControlReply> ResetOwningControllerProcessConfAsync(CancellationToken cancellationToken = default)
			=> SendCommandAsync("RESETCONF __OwningControllerProcess\r\n", cancellationToken);

		/// <summary>
		/// Sends a command to Tor.
		/// </summary>
		/// <remarks>This is meant as a low-level API method, if needed for some reason.</remarks>
		/// <param name="command">A Tor control command which must end with <c>\r\n</c>.</param>
		public async Task<TorControlReply> SendCommandAsync(string command, CancellationToken cancellationToken = default)
		{
			using var _ = await MessageLock.LockAsync(cancellationToken).ConfigureAwait(false);

			Logger.LogTrace($"Client: About to send command: '{command.TrimEnd()}'");
			await PipeWriter.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(command)), cancellationToken).ConfigureAwait(false);
			await PipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

			TorControlReply reply = await SyncChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
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
					newList = new(AsyncChannels);
					newList.Add(channel);

					AsyncChannels = newList;
				}

				Logger.LogTrace($"ReadEventsAsync: subscribers: {newList.Count}.");

				await foreach (TorControlReply item in channel.Reader.ReadAllAsync(cancellationToken))
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
					newList.Remove(channel);

					AsyncChannels = newList;
				}
			}
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
					}
				}
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Reader loop was stopped.");
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
			// Stop reader loop.
			ReaderCts.Cancel();

			// Wait until the reader loop stops.
			await ReaderLoopTask.ConfigureAwait(false);

			TcpClient?.Dispose();
		}
	}
}
