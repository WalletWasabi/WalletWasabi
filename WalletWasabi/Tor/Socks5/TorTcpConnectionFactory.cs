using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
	/// Creates a new connected TCP client connected to Tor SOCKS5 endpoint.
	/// </summary>
	/// <inheritdoc cref="ConnectAsync(string, int, bool, ICircuit, CancellationToken)"/>
	public virtual async Task<TorTcpConnection> ConnectAsync(Uri requestUri, ICircuit circuit, CancellationToken token = default)
	{
		bool useSsl = requestUri.Scheme == Uri.UriSchemeHttps;
		string host = requestUri.DnsSafeHost;
		int port = requestUri.Port;

		return await ConnectAsync(host, port, useSsl, circuit, token).ConfigureAwait(false);
	}

	/// <summary>
	/// Creates a new connected TCP client connected to Tor SOCKS5 endpoint.
	/// </summary>
	/// <param name="host">Tor SOCKS5 host.</param>
	/// <param name="port">Tor SOCKS5 port.</param>
	/// <param name="useSsl">Whether to use SSL to send the HTTP request over Tor.</param>
	/// <param name="circuit">Tor circuit we want to use in authentication.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
	/// <returns>New <see cref="TorTcpConnection"/> instance.</returns>
	/// <exception cref="TorConnectionException">When <see cref="ConnectAsync(TcpClient, CancellationToken)"/> fails.</exception>
	public async Task<TorTcpConnection> ConnectAsync(string host, int port, bool useSsl, ICircuit circuit, CancellationToken cancellationToken = default)
	{
		TcpClient? tcpClient = null;
		Stream? transportStream = null;

		try
		{
			tcpClient = new(TorSocks5EndPoint.AddressFamily);
			tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

			// Windows 7 does not support the API we use.
			if (!(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version.Major < 10))
			{
				try
				{
					tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
					tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
					tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
				}
				catch (SocketException ex) when (ex.ErrorCode is 10042)
				{
					Logger.LogWarning("KeepAlive settings are not allowed by your OS. Ignoring.");
				}
			}

			transportStream = await ConnectAsync(tcpClient, cancellationToken).ConfigureAwait(false);
			await HandshakeAsync(tcpClient, circuit, cancellationToken).ConfigureAwait(false);
			await ConnectToDestinationAsync(tcpClient, host, port, cancellationToken).ConfigureAwait(false);

			if (useSsl)
			{
				transportStream = await UpgradeToSslAsync(tcpClient, host).ConfigureAwait(false);
			}

			bool allowRecycling = !useSsl && (circuit is DefaultCircuit or PersonCircuit);
			TorTcpConnection result = new(tcpClient, transportStream, circuit, allowRecycling);

			transportStream = null;
			tcpClient = null;
			return result;
		}
		finally
		{
			transportStream?.Dispose();
			tcpClient?.Dispose();
		}
	}

	/// <summary>
	/// Establishes TCP connection with Tor SOCKS5 endpoint.
	/// </summary>
	/// <exception cref="ArgumentException">This should never happen.</exception>
	/// <exception cref="TorException">When connection to Tor SOCKS5 endpoint fails.</exception>
	private async Task<NetworkStream> ConnectAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
	{
		try
		{
			await tcpClient.ConnectAsync(TorHost, TorPort, cancellationToken).ConfigureAwait(false);
			return tcpClient.GetStream();
		}
		catch (SocketException ex) when (ex.ErrorCode is 10061 or 111 or 61)
		{
			// 10061 ~ "No connection could be made because the target machine actively refused it" on Windows.
			// 111   ~ "Connection refused" on Linux.
			// 61    ~ "Connection refused" on macOS.
			throw new TorConnectionException($"Could not connect to Tor SOCKSPort at '{TorHost}:{TorPort}'. Is Tor running?", ex);
		}
	}

	/// <summary>
	/// Checks whether communication can be established with Tor over <see cref="TorSocks5EndPoint"/> endpoint.
	/// </summary>
	public virtual async Task<bool> IsTorRunningAsync()
	{
		try
		{
			// Internal TCP client may close, so we need a new instance here.
			using TcpClient tcpClient = new(TorSocks5EndPoint.AddressFamily);
			await ConnectAsync(tcpClient).ConfigureAwait(false);
			await HandshakeAsync(tcpClient, DefaultCircuit.Instance).ConfigureAwait(false);

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
	/// <exception cref="NotSupportedException">When authentication fails due to unsupported authentication method.</exception>
	/// <exception cref="InvalidOperationException">When authentication fails due to invalid credentials.</exception>
	private async Task HandshakeAsync(TcpClient tcpClient, ICircuit circuit, CancellationToken cancellationToken = default)
	{
		// https://github.com/torproject/torspec/blob/master/socks-extensions.txt
		// The "NO AUTHENTICATION REQUIRED" (SOCKS5) authentication method [00] is
		// supported; and as of Tor 0.2.3.2 - alpha, the "USERNAME/PASSWORD"(SOCKS5)
		// authentication method[02] is supported too, and used as a method to
		// implement stream isolation.As an extension to support some broken clients,
		// we allow clients to pass "USERNAME/PASSWORD" authentication message to us
		// even if no authentication was selected.Furthermore, we allow
		// username / password fields of this message to be empty. This technically
		// violates RFC1929[4], but ensures interoperability with somewhat broken
		// SOCKS5 client implementations.
		MethodsField methods = new(MethodField.UsernamePassword);

		byte[] receiveBuffer = await SendRequestAsync(tcpClient, new VersionMethodRequest(methods), cancellationToken).ConfigureAwait(false);

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
		else if (methodSelection.Method == MethodField.UsernamePassword)
		{
			// https://tools.ietf.org/html/rfc1929#section-2
			// Once the SOCKS V5 server has started, and the client has selected the
			// Username / Password Authentication protocol, the Username / Password
			// sub-negotiation begins. This begins with the client producing a
			// Username / Password request:
			UNameField uName = new(uName: circuit.Name);
			PasswdField passwd = new(password: circuit.Name);
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

	private static async Task<SslStream> UpgradeToSslAsync(TcpClient tcpClient, string host)
	{
		SslStream sslStream = new(tcpClient.GetStream(), leaveInnerStreamOpen: true);
		await sslStream.AuthenticateAsClientAsync(host, clientCertificates: new(), checkCertificateRevocation: true).ConfigureAwait(false);
		return sslStream;
	}

	/// <summary>
	/// Sends <see cref="CmdField.Connect"/> command to SOCKS5 server to instruct it to connect to
	/// <paramref name="host"/>:<paramref name="port"/> on behalf of this client.
	/// </summary>
	/// <param name="host">IPv4 or domain of the destination.</param>
	/// <param name="port">Port number of the destination.</param>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	/// <exception cref="TorConnectCommandFailedException">When response to <see cref="CmdField.Connect"/> request is NOT <see cref="RepField.Succeeded"/>.</exception>
	/// <exception cref="TorException">When sending of the HTTP(s) request fails for any reason.</exception>
	/// <seealso href="https://tools.ietf.org/html/rfc1928">Section 3. Procedure for TCP-based clients</seealso>
	private async Task ConnectToDestinationAsync(TcpClient tcpClient, string host, int port, CancellationToken cancellationToken = default)
	{
		Logger.LogTrace($"> {nameof(host)}='{host}', {nameof(port)}={port}");

		host = Guard.NotNullOrEmptyOrWhitespace(nameof(host), host, trim: true);
		Guard.MinimumAndNotNull(nameof(port), port, smallest: 0);

		try
		{
			TorSocks5Request connectionRequest = new(cmd: CmdField.Connect, new(host), new(port));

			byte[] receiveBuffer = await SendRequestAsync(tcpClient, connectionRequest, cancellationToken).ConfigureAwait(false);

			TorSocks5Response connectionResponse = new(receiveBuffer);

			if (connectionResponse.Rep != RepField.Succeeded)
			{
				// https://www.ietf.org/rfc/rfc1928.txt
				// When a reply (REP value other than X'00') indicates a failure, the
				// SOCKS server MUST terminate the TCP connection shortly after sending
				// the reply. This must be no more than 10 seconds after detecting the
				// condition that caused a failure.
				Logger.LogWarning($"Connection response indicates a failure. Actual response is: '{connectionResponse.Rep}'. Request: '{host}:{port}'.");
				throw new TorConnectCommandFailedException(connectionResponse.Rep);
			}

			// Do not check the Bnd. Address and Bnd. Port. because Tor does not seem to return any, ever. It returns zeros instead.
			// Generally also do not check anything but the success response, according to Socks5 RFC.

			// If the reply code(REP value of X'00') indicates a success, and the
			// request was either a BIND or a CONNECT, the client may now start
			// passing data. If the selected authentication method supports
			// encapsulation for the purposes of integrity, authentication and / or
			// confidentiality, the data are encapsulated using the method-dependent
			// encapsulation.Similarly, when data arrives at the SOCKS server for
			// the client, the server MUST encapsulate the data as appropriate for
			// the authentication method in use.
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace($"Connecting to destination '{host}:{port}' was canceled.");
			throw;
		}
		catch (TorException e)
		{
			Logger.LogError($"Exception occurred when connecting to '{host}:{port}'.", e);
			throw;
		}
		finally
		{
			Logger.LogTrace("<");
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
	private async Task<byte[]> SendRequestAsync(TcpClient tcpClient, ByteArraySerializableBase request, CancellationToken cancellationToken = default)
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
