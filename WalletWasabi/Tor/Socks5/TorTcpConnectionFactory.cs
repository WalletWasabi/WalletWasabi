using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Models.Messages;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Socks5;

/// <summary>
/// Factory class to create new <see cref="TorTcpConnection"/> instances.
/// </summary>
public class TorTcpConnectionFactory
{
	private static readonly VersionMethodRequest VersionMethodNoAuthRequired = new(methods: new MethodsField(MethodField.NoAuthenticationRequired));
	private static readonly VersionMethodRequest VersionMethodUsernamePassword = new(methods: new MethodsField(MethodField.UsernamePassword));

	/// <param name="endPoint">Tor SOCKS5 endpoint.</param>
	public TorTcpConnectionFactory(EndPoint endPoint)
	{
		TorSocks5EndPoint = endPoint;
		if (TorSocks5EndPoint.TryGetHostAndPort(out var host, out var port))
		{
			TorHost = host;
			TorPort = port.Value;
		}
		else
		{
			throw new ArgumentException("Endpoint type is not supported.", nameof(endPoint));
		}
	}

	private EndPoint TorSocks5EndPoint { get; }
	private string TorHost { get; }
	private int TorPort { get; }

	/// <summary>
	/// Checks whether communication can be established with Tor over <see cref="TorSocks5EndPoint"/> endpoint.
	/// </summary>
	public virtual async Task<bool> IsTorRunningAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Internal TCP client may close, so we need a new instance here.
			using TcpClient tcpClient = await TcpClientSocks5Connector.ConnectAsync(TorSocks5EndPoint, cancellationToken).ConfigureAwait(false);
			await HandshakeAsync(tcpClient, DefaultCircuit.Instance, cancellationToken).ConfigureAwait(false);

			return true;
		}
		catch (TorConnectionException)
		{
			return false;
		}
	}

	/// <summary>
	/// Do the authentication part of Tor's SOCKS5 protocol.
	/// </summary>
	/// <param name="circuit">Tor circuit we want to use in authentication.</param>
	/// <remarks>Tor process must be started with enabled <c>IsolateSOCKSAuth</c> option. It's ON by default.</remarks>
	/// <seealso href="https://www.torproject.org/docs/tor-manual.html.en"/>
	/// <seealso href="https://linux.die.net/man/1/tor">For <c>IsolateSOCKSAuth</c> option explanation.</seealso>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n35"/>
	/// <seealso href="https://github.com/torproject/torspec/blob/79da008392caed38736c73d839df7aa80628b645/socks-extensions.txt#L49-L51">Explains why we pass username and password even for <see cref="MethodField.NoAuthenticationRequired"/>.</seealso>
	/// <exception cref="NotSupportedException">When authentication fails due to unsupported authentication method.</exception>
	/// <exception cref="InvalidOperationException">When authentication fails due to invalid credentials.</exception>
	private async Task HandshakeAsync(TcpClient tcpClient, INamedCircuit circuit, CancellationToken cancellationToken)
	{
		VersionMethodRequest versionMethodRequest = circuit switch
		{
			DefaultCircuit => VersionMethodNoAuthRequired,
			_ => VersionMethodUsernamePassword
		};

		byte[] receiveBuffer = await SendRequestAsync(tcpClient, versionMethodRequest, cancellationToken).ConfigureAwait(false);

		MethodSelectionResponse methodSelection = new(receiveBuffer);

		if (methodSelection.Ver != VerField.Socks5)
		{
			throw new NotSupportedException($"SOCKS{methodSelection.Ver.Value} not supported. Only SOCKS5 is supported.");
		}
		else if (methodSelection.Method == MethodField.NoAcceptableMethods)
		{
			// https://www.ietf.org/rfc/rfc1928.txt
			// If the selected METHOD is X'FF', none of the methods listed by the
			// client are acceptable, and the client MUST close the connection.
			throw new NotSupportedException("Tor's SOCKS5 proxy does not support any of the client's authentication methods.");
		}
		else if (methodSelection.Method == MethodField.UsernamePassword || methodSelection.Method == MethodField.NoAuthenticationRequired)
		{
			// Regarding NoAuthenticationRequired: Tor spec explicitly mentions that username & password can be passed even if no authentication is required.
			// Tor does that to allow broken clients to work. However, for us, it is important to mark Tor streams somehow so that we know when the streams are
			// closed. Unfortunately, using a non-standard Tor SOCKS5 feature is the easiest way to do it. Otherwise, the implementation would get much more hairy.
			// That is probably the reason why Tor control protocol is not intended to be a part of Tor (rust) implementation.

			UNameField uName = new(uName: circuit.Name);
			PasswdField passwd = new(password: $"{circuit.IsolationId}");
			UsernamePasswordRequest usernamePasswordRequest = new(uName, passwd);

			receiveBuffer = await SendRequestAsync(tcpClient, usernamePasswordRequest, cancellationToken).ConfigureAwait(false);

			UsernamePasswordResponse userNamePasswordResponse = new(receiveBuffer);

			if (userNamePasswordResponse.Ver != usernamePasswordRequest.Ver)
			{
				throw new NotSupportedException($"Authentication version {userNamePasswordResponse.Ver.Value} not supported. Only version {usernamePasswordRequest.Ver} is supported.");
			}

			if (!userNamePasswordResponse.Status.IsSuccess()) // Tor authentication is different, this will never happen;
			{
				// https://tools.ietf.org/html/rfc1929#section-2
				// A STATUS field of X'00' indicates success. If the server returns a
				// `failure' (STATUS value other than X'00') status, it MUST close the
				// connection.
				throw new InvalidOperationException("Wrong username and/or password.");
			}
		}
	}

	/// <summary>
	/// Sends a request to the Tor SOCKS5 connection and returns a byte response.
	/// </summary>
	/// <param name="request">Request to send.</param>
	/// <param name="cancellationToken">Cancellation token to cancel sending.</param>
	/// <returns>Reply</returns>
	/// <exception cref="ArgumentException">When <paramref name="request"/> is not supported.</exception>
	/// <exception cref="TorConnectionException">When we receive no response from Tor or the response is invalid.</exception>
	private async Task<byte[]> SendRequestAsync(TcpClient tcpClient, ByteArraySerializableBase request, CancellationToken cancellationToken)
	{
		try
		{
			NetworkStream stream = tcpClient.GetStream();

			byte[] dataToSend = request.ToBytes();

			// Write data to the stream.
			await stream.WriteAsync(dataToSend.AsMemory(0, dataToSend.Length), cancellationToken).ConfigureAwait(false);
			await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

			if (request is VersionMethodRequest or UsernamePasswordRequest)
			{
				return await ReadTwoByteResponseAsync(stream, cancellationToken).ConfigureAwait(false);
			}
			else if (request is TorSocks5Request)
			{
				return await ReadRequestResponseAsync(stream, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				throw new ArgumentException("Not supported request type.", nameof(request));
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace("Send operation was canceled.");
			throw;
		}
		catch (IOException ex)
		{
			throw new TorConnectionException($"{nameof(TorTcpConnectionFactory)} is not connected to the remote endpoint.", ex);
		}
	}

	/// <summary>
	/// Reads response for <see cref="TorSocks5Request"/>.
	/// </summary>
	private static async Task<byte[]> ReadRequestResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
	{
		ByteArrayBuilder builder = new(capacity: 1024);

		// Read: VER, CMD, RSV and ATYP values.
		int byteResult = -1;

		for (int i = 0; i < 4; i++)
		{
			byteResult = await stream.ReadByteAsync(cancellationToken).ConfigureAwait(false);

			if (byteResult == -1)
			{
				throw new TorConnectionException("Failed to read first four bytes from the SOCKS5 response.");
			}

			builder.Append((byte)byteResult);
		}

		// Process last read byte which is ATYP.
		byte addrType = (byte)byteResult;

		int dstAddrLength = addrType switch
		{
			// IPv4.
			0x01 => 4,

			// Fully-qualified domain name.
			0x03 => await stream.ReadByteAsync(cancellationToken).ConfigureAwait(false),

			// IPv6.
			0x04 => 16,
			_ => throw new TorConnectionException("Received unsupported ATYP value.")
		};

		if (dstAddrLength == -1)
		{
			throw new TorConnectionException("Failed to read the length of DST.ADDR from the SOCKS5 response.");
		}

		// Read DST.ADDR.
		for (int i = 0; i < dstAddrLength; i++)
		{
			byteResult = await stream.ReadByteAsync(cancellationToken).ConfigureAwait(false);

			if (byteResult == -1)
			{
				throw new TorConnectionException("Failed to read DST.ADDR from the SOCKS5 response.");
			}

			builder.Append((byte)byteResult);
		}

		// Read DST.PORT.
		for (int i = 0; i < 2; i++)
		{
			byteResult = await stream.ReadByteAsync(cancellationToken).ConfigureAwait(false);

			if (byteResult == -1)
			{
				throw new TorConnectionException("Failed to read DST.PORT from the SOCKS5 response.");
			}

			builder.Append((byte)byteResult);
		}

		return builder.ToArray();
	}

	private static async Task<byte[]> ReadTwoByteResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
	{
		// Read exactly "receiveBufferSize" bytes.
		int receiveBufferSize = 2;
		byte[] receiveBuffer = new byte[receiveBufferSize];

		int unreadBytes = await stream.ReadBlockAsync(receiveBuffer, receiveBufferSize, cancellationToken).ConfigureAwait(false);

		if (unreadBytes == receiveBufferSize)
		{
			return receiveBuffer;
		}

		throw new TorConnectionException($"Failed to read {receiveBufferSize} bytes as expected from Tor SOCKS5.");
	}
}
