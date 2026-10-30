using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Coordinator.Services;

/// <summary>
/// Subscribes ZeroMQ <c>hashblock</c> events from a running Bitcoin Core node using ZeroMQ Message Transfer Protocol (ZMTP).
/// </summary>
/// <remarks>Run your bitcoind like this to test this feature: <c>./bitcoind -zmqpubhashblock=tcp://127.0.0.1:28332</c>.</remarks>
/// <seealso href="https://rfc.zeromq.org/spec/23/"/>
/// <seealso href="https://github.com/bitcoin/bitcoin/blob/master/doc/zmq.md"/>
public class ZmtpBlockHashSubscriber : BackgroundService
{
	private const string Topic = "hashblock";
	private static readonly byte[] TopicBytes = System.Text.Encoding.ASCII.GetBytes(Topic);
	private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

	/// <summary>Greeting to send from client to server supporting version 3.0 or higher.</summary>
	/// <remarks>It contains: signature, version 3.0, mechanism "NULL", as-server = 0.</remarks>
	/// <seealso href="https://rfc.zeromq.org/spec/23/#version-negotiation"/>
	private static readonly byte[] GreetingV3 = [
		// Bytes 0-9: Signature.
		0xFF,                          // Byte 0: Fixed signature start marker.
		0, 0, 0, 0, 0, 0, 0, 0,        // Bytes 1-8: Padding that is ignored by the peer.
		0x7F,                          // Byte 9: Fixed signature end marker.

		// Bytes 10-11: Version.
		3,                             // Byte 10: ZMTP major version.
		0,                             // Byte 11: ZMTP minor version.

		// Bytes 12-31: Security mechanism name, ASCII, right-padded with zeros to 20 bytes.
		0x4E, 0x55, 0x4C, 0x4C,        // Bytes 12-15: "NULL".
		0, 0, 0, 0, 0, 0, 0, 0,        // Bytes 16-23: Mechanism name padding.
		0, 0, 0, 0, 0, 0, 0, 0,        // Bytes 24-31: Mechanism name padding.

		// Byte 32: As-server flag.
		0,                             // Byte 32: 0x00 = not server, 0x01 = server. Ignored for the NULL mechanism.

		// Bytes 33-63: Filler, reserved for future use and must be zero.
		0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
	];

	public ZmtpBlockHashSubscriber(string host, int port, ChannelWriter<ZmqEvent> outputChannel)
	{
		Host = host;
		Port = port;
		OutputChannel = outputChannel;
	}

	public string Host { get; }
	public int Port { get; }
	public ChannelWriter<ZmqEvent> OutputChannel { get; }

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogInfo($"The service was started to subscribe to ZMQ at {Host}:{Port}.");
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		var reconnectionDelay = TimeSpan.FromSeconds(1);

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					using var networkStream = await ConnectAsync(Host, Port, cancellationToken).ConfigureAwait(false);
					await HandshakeAsync(networkStream, cancellationToken).ConfigureAwait(false);
					reconnectionDelay = TimeSpan.FromSeconds(1);

					await SubscribeAsync(networkStream, OutputChannel, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break; // Real shutdown.
				}
				catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
				{
					Logger.LogWarning($"Connection was actively refused. Is bitcoind running with -zmqpubhashblock=tcp://{Host}:{Port}? Retrying in {reconnectionDelay.TotalSeconds:0}s.");
					Logger.LogWarning(ex);
				}
				catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
				{
					// OperationCanceledException here can only be the handshake timeout; shutdown is handled above.
					Logger.LogWarning($"Connection lost or not established. Retrying in {reconnectionDelay.TotalSeconds:0}s.");
					Logger.LogWarning(ex);
				}

				await Task.Delay(reconnectionDelay, cancellationToken).ConfigureAwait(false);
				reconnectionDelay = TimeSpan.FromSeconds(Math.Min(reconnectionDelay.TotalSeconds * 2, 30));
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError("The service crashed.", ex);
			OutputChannel.TryComplete(ex);
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogInfo("The service was stopped.");
	}

	private static async Task<NetworkStream> ConnectAsync(string host, int port, CancellationToken cancellationToken)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope – Owned by the network stream or disposed.
		var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
#pragma warning restore CA2000 // Dispose objects before losing scope

		try
		{
			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);

			await socket.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Handshake with the remote server to verify that client and server are compatible.
	/// </summary>
	/// <remarks><see cref="NotSupportedException"/> and <see cref="InvalidDataException"/> are fatal exceptions. Other exceptions are not.</remarks>
	private static async Task HandshakeAsync(NetworkStream stream, CancellationToken cancellationToken)
	{
		// Send greeting to a server supporting ZMTP v3 (https://rfc.zeromq.org/spec/23/#version-negotiation).
		await stream.WriteAsync(GreetingV3, cancellationToken).ConfigureAwait(false);

		// Wait for a greeting response.
		byte[] peerGreeting = new byte[64];

		using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
		{
			handshake.CancelAfter(HandshakeTimeout);
			await stream.ReadExactlyAsync(peerGreeting, handshake.Token).ConfigureAwait(false);
		}

		// Signature, major version and mechanism ("NULL" plus padding, bytes 12-31).
		if (peerGreeting[0] != 0xFF || peerGreeting[9] != 0x7F || peerGreeting[10] < 3
			|| !peerGreeting.AsSpan(12, 20).SequenceEqual(GreetingV3.AsSpan(12, 20)))
		{
			throw new InvalidDataException("The peer is not a ZMTP 3.x server using the NULL mechanism.");
		}
	}

	/// <summary>
	/// Subscribes <c>hashblock</c> events from the ZMQ server.
	/// </summary>
	/// <remarks><see cref="NotSupportedException"/> and <see cref="InvalidDataException"/> are fatal exceptions. Other exceptions are not.</remarks>
	private static async Task SubscribeAsync(NetworkStream stream, ChannelWriter<ZmqEvent> outputWriter, CancellationToken cancellationToken)
	{
		// READY command with Socket-Type=SUB.
		// https://rfc.zeromq.org/spec/23/#the-null-security-mechanism
		byte[] ready = [5, .. "READY"u8, 11, .. "Socket-Type"u8, 0, 0, 0, 3, .. "SUB"u8];
		byte[] readyFrame = [0x04, (byte)ready.Length, .. ready];

		byte[] subFrame = [0x00, (byte)(1 + Topic.Length), 0x01, .. TopicBytes];

		// Subscribe only "hashblock".
		await stream.WriteAsync((byte[])[.. readyFrame, .. subFrame], cancellationToken).ConfigureAwait(false);

		// Report over the channel that the subscription starts at this moment with a special message.
		await outputWriter.WriteAsync(ZmqEvent.SubscriptionStarts, cancellationToken).ConfigureAwait(false);

		var parts = new List<byte[]>();
		var header = new byte[2];

		while (true)
		{
			// https://rfc.zeromq.org/spec/23/#framing
			await stream.ReadExactlyAsync(header.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);
			var flags = header[0];

			if ((flags & 0x02) != 0)
			{
				// Intentionally fatal: this client only expects the tiny frames of the "hashblock" topic.
				throw new NotSupportedException("Long ZMQ frames are not supported.");
			}

			int len = header[1];
			var body = new byte[len];
			await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);

			// Command frame.
			if ((flags & 0x04) != 0)
			{
				continue;
			}

			if (parts.Count >= 3)
			{
				throw new InvalidDataException("Too many ZMQ message frames.");
			}

			parts.Add(body);

			// More frames to come.
			if ((flags & 0x01) != 0)
			{
				continue;
			}

			// Complete message: accept only [ "hashblock", 32-byte hash, 4-byte sequence ].
			if (parts is [{ } topic, { Length: 32 } hash, { Length: 4 } seq]
				&& topic.AsSpan().SequenceEqual(TopicBytes))
			{
				var blockHash = uint256.Parse(Convert.ToHexStringLower(hash));
				uint sequence = BinaryPrimitives.ReadUInt32LittleEndian(seq);
				var @event = new ZmqEvent(blockHash, sequence);
				await outputWriter.WriteAsync(@event, cancellationToken).ConfigureAwait(false);
			}

			parts.Clear();
		}
	}
}

/// <remarks>When <paramref name="Hash"/> is null, it means "just (re)connected".</remarks>
public readonly record struct ZmqEvent(uint256? Hash, uint Sequence)
{
	public static readonly ZmqEvent SubscriptionStarts = new(Hash: null, Sequence: 0);
}
