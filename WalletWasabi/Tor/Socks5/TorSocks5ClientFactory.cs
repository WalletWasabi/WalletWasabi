using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Models.Messages;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// Factory class to create new <see cref="TorConnection"/> instances.
	/// </summary>
	public class TorSocks5ClientFactory
	{
		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="endPoint">Tor SOCKS5 endpoint.</param>
		public TorSocks5ClientFactory(EndPoint endPoint)
		{
			TorSocks5EndPoint = endPoint;
		}

		private EndPoint TorSocks5EndPoint { get; }

		/// <summary>
		/// Checks whether communication can be established with Tor over <see cref="TorSocks5EndPoint"/> endpoint.
		/// </summary>
		/// <returns></returns>
		public async Task<bool> IsTorRunningAsync()
		{
			try
			{
				// Internal TCP client may close, so we need a new instance here.
				using var tcpClient = new TcpClient(TorSocks5EndPoint.AddressFamily);
				await ConnectAsync(tcpClient).ConfigureAwait(false);
				await HandshakeAsync(tcpClient, isolateStream: false).ConfigureAwait(false);

				return true;
			}
			catch (TorConnectionException)
			{
				return false;
			}
			catch (TorAuthenticationException)
			{
				return false;
			}
		}

		/// <summary>
		/// Creates a new connected TCP client connected to Tor SOCKS5 endpoint.
		/// </summary>
		/// <param name="isolateStream"></param>
		/// <param name="host">Tor SOCKS5 host.</param>
		/// <param name="port">Tor SOCKS5 port.</param>
		/// <param name="useSsl">Whether to use SSL to send the HTTP request over Tor.</param>
		/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
		/// <returns>New <see cref="TorConnection"/> instance.</returns>
		/// <exception cref="TorConnection"/>
		/// <exception cref="TorAuthenticationException"/>
		/// <exception cref="TorHttpException"/>
		public async Task<TorConnection> MakeAsync(bool isolateStream, string host, int port, bool useSsl, CancellationToken cancellationToken = default)
		{
			TcpClient? tcpClient = null;
			Stream? transportStream = null;

			try
			{
				tcpClient = new TcpClient(TorSocks5EndPoint.AddressFamily);

				transportStream = await ConnectAsync(tcpClient).ConfigureAwait(false);
				await HandshakeAsync(tcpClient, isolateStream, cancellationToken).ConfigureAwait(false);
				await ConnectToDestinationAsync(tcpClient, host, port, cancellationToken).ConfigureAwait(false);

				if (useSsl)
				{
					transportStream = await UpgradeToSslAsync(tcpClient, host).ConfigureAwait(false);
				}

				var result = new TorConnection(tcpClient, transportStream);

				transportStream = null;
				tcpClient = null;
				return result;
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				transportStream?.Dispose();
				tcpClient?.Dispose();
			}
		}

		private async Task<SslStream> UpgradeToSslAsync(TcpClient tcpClient, string host)
		{
			var sslStream = new SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false);
			await sslStream.AuthenticateAsClientAsync(host, new X509CertificateCollection(), IHttpClient.SupportedSslProtocols, true).ConfigureAwait(false);
			return sslStream;
		}

		/// <summary>
		/// Establishes TCP connection with Tor SOCKS5 endpoint.
		/// </summary>
		/// <exception cref="ArgumentException">This should never happen.</exception>
		/// <exception cref="TorAuthenticationException">When connection to Tor SOCKS5 endpoint fails.</exception>
		private async Task<NetworkStream> ConnectAsync(TcpClient tcpClient)
		{
			if (!TorSocks5EndPoint.TryGetHostAndPort(out string? host, out int? port))
			{
				throw new ArgumentException("Endpoint type is not supported.", nameof(TorSocks5EndPoint));
			}

			try
			{
				// Cancellation token for ConnectAsync will be available in .NET 5.
				await tcpClient.ConnectAsync(host, port.Value).ConfigureAwait(false);
				return tcpClient.GetStream();
			}
			catch (Exception ex) when (IsConnectionRefused(ex))
			{
				throw new TorConnectionException($"Could not connect to Tor SOCKSPort at '{host}:{port}'. Is Tor running?", ex);
			}
		}

		/// <summary>
		/// Do the authentication part of Tor's SOCKS5 protocol.
		/// </summary>
		/// <param name="isolateStream">Whether random username/password should be used for authentication and thus effectively create a new Tor circuit.</param>
		/// <remarks>Tor process must be started with enabled <c>IsolateSOCKSAuth</c> option. It's ON by default.</remarks>
		/// <seealso href="https://www.torproject.org/docs/tor-manual.html.en"/>
		/// <seealso href="https://linux.die.net/man/1/tor">For <c>IsolateSOCKSAuth</c> option explanation.</seealso>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n35"/>
		private async Task HandshakeAsync(TcpClient tcpClient, bool isolateStream = false, CancellationToken cancellationToken = default)
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
			var methods = new MethodsField(isolateStream ? MethodField.UsernamePassword : MethodField.NoAuthenticationRequired);

			byte[] sendBuffer = new VersionMethodRequest(methods).ToBytes();
			byte[] receiveBuffer = await SendAsync(tcpClient, sendBuffer, receiveBufferSize: 2, cancellationToken).ConfigureAwait(false);

			var methodSelection = new MethodSelectionResponse(receiveBuffer);

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
				var identity = RandomString.CapitalAlphaNumeric(21);
				var uName = new UNameField(uName: identity);
				var passwd = new PasswdField(passwd: identity);
				var usernamePasswordRequest = new UsernamePasswordRequest(uName, passwd);
				sendBuffer = usernamePasswordRequest.ToBytes();

				Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
				receiveBuffer = await SendAsync(tcpClient, sendBuffer, receiveBufferSize: 2, cancellationToken).ConfigureAwait(false);

				var userNamePasswordResponse = new UsernamePasswordResponse(receiveBuffer);

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
		/// Sends <see cref="CmdField.Connect"/> command to SOCKS5 server to instruct it to connect to
		/// <paramref name="host"/>:<paramref name="port"/> on behalf of this client.
		/// </summary>
		/// <param name="host">IPv4 or domain of the destination.</param>
		/// <param name="port">Port number of the destination.</param>
		/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
		/// <exception cref="TorHttpResponseException">When response to <see cref="CmdField.Connect"/> request is NOT <see cref="RepField.Succeeded"/>.</exception>
		/// <exception cref="TorHttpException">When sending of the HTTP(s) request fails for any reason.</exception>
		/// <seealso href="https://tools.ietf.org/html/rfc1928">Section 3. Procedure for TCP-based clients</seealso>
		private async Task ConnectToDestinationAsync(TcpClient tcpClient, string host, int port, CancellationToken cancellationToken = default)
		{
			Logger.LogDebug($"> {nameof(host)}='{host}', {nameof(port)}={port}");

			host = Guard.NotNullOrEmptyOrWhitespace(nameof(host), host, trim: true);
			Guard.MinimumAndNotNull(nameof(port), port, smallest: 0);

			try
			{
				var connectionRequest = new TorSocks5Request(cmd: CmdField.Connect, new AddrField(host), new PortField(port));
				var sendBuffer = connectionRequest.ToBytes();

				var receiveBuffer = await SendAsync(tcpClient, sendBuffer, receiveBufferSize: null, cancellationToken).ConfigureAwait(false);

				var connectionResponse = new TorSocks5Response(receiveBuffer);

				if (connectionResponse.Rep != RepField.Succeeded)
				{
					// https://www.ietf.org/rfc/rfc1928.txt
					// When a reply (REP value other than X'00') indicates a failure, the
					// SOCKS server MUST terminate the TCP connection shortly after sending
					// the reply. This must be no more than 10 seconds after detecting the
					// condition that caused a failure.
					Logger.LogWarning($"Connection response indicates a failure. Actual response is: '{connectionResponse.Rep}'. Request: '{host}:{port}'.");
					throw new TorHttpResponseException(connectionResponse.Rep);
				}

				// Do not check the Bnd. Address and Bnd. Port. because Tor does not seem to return any, ever. It returns zeros instead.
				// Generally also do not check anything but the success response, according to Socks5 RFC

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
			catch (TorHttpResponseException e)
			{
				Logger.LogError($"{nameof(TorHttpResponseException)} was thrown when connecting to destination '{host}:{port}'.", e);
				throw;
			}
			catch (Exception e)
			{
				Logger.LogError($"Exception was thrown when connecting to destination '{host}:{port}'.", e);
				throw new TorHttpException($"Failed to connect to '{host}:{port}'", e);
			}
			finally
			{
				Logger.LogDebug("<");
			}
		}

		private bool IsConnectionRefused(Exception exc)
		{
			Exception? error = null;
			try
			{
				throw exc;
			}
			// ex.Message must be checked, because I'm having difficulty catching SocketExceptionFactory+ExtendedSocketException
			// Only works on English Os-es.
			catch (Exception ex) when (ex.Message.StartsWith("No connection could be made because the target machine actively refused it") // Windows
				|| ex.Message.StartsWith("Connection refused")) // Linux && OSX
			{
				error = ex;
			}
			// "No connection could be made because the target machine actively refused it" for non-English Windows.
			catch (SocketException ex) when (ex.ErrorCode == 10061)
			{
				error = ex;
			}
			// "Connection refused" for non-English Linux.
			catch (SocketException ex) when (ex.ErrorCode == 111)
			{
				error = ex;
			}
			// "Connection refused" for non-English OSX.
			catch (SocketException ex) when (ex.ErrorCode == 61)
			{
				error = ex;
			}
			catch
			{
				// Ignored, since error is null.
			}

			return error is { };
		}

		/// <summary>
		/// Sends bytes to the Tor Socks5 connection.
		/// </summary>
		/// <param name="sendBuffer">Sent data</param>
		/// <param name="receiveBufferSize">Maximum number of bytes expected to be received in the reply.</param>
		/// <param name="cancellationToken">Cancellation token to cancel sending.</param>
		/// <returns>Reply</returns>
		private async Task<byte[]> SendAsync(TcpClient tcpClient, byte[] sendBuffer, int? receiveBufferSize = null, CancellationToken cancellationToken = default)
		{
			Guard.NotNullOrEmpty(nameof(sendBuffer), sendBuffer);

			try
			{
				var stream = tcpClient.GetStream();

				// Write data to the stream
				await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length, cancellationToken).ConfigureAwait(false);
				await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

				// If receiveBufferSize is null, zero or negative or bigger than TcpClient.ReceiveBufferSize
				// then work with TcpClient.ReceiveBufferSize
				var tcpReceiveBuffSize = tcpClient.ReceiveBufferSize;
				var actualReceiveBufferSize = receiveBufferSize is null || receiveBufferSize <= 0 || receiveBufferSize > tcpReceiveBuffSize
					? tcpReceiveBuffSize
					: (int)receiveBufferSize;

				// Receive the response
				var receiveBuffer = new byte[actualReceiveBufferSize];

				int receiveCount = await stream.ReadAsync(receiveBuffer, 0, actualReceiveBufferSize, cancellationToken).ConfigureAwait(false);

				if (receiveCount <= 0)
				{
					throw new TorConnectionException($"Not connected to Tor SOCKS5 proxy: {TorSocks5EndPoint}.");
				}
				// if we could fit everything into our buffer, then return it
				if (!stream.DataAvailable)
				{
					return receiveBuffer[..receiveCount];
				}

				// while we have data available, start building a byte array
				var builder = new ByteArrayBuilder();
				builder.Append(receiveBuffer[..receiveCount]);
				while (stream.DataAvailable)
				{
					Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
					receiveCount = await stream.ReadAsync(receiveBuffer, 0, actualReceiveBufferSize, cancellationToken).ConfigureAwait(false);
					if (receiveCount <= 0)
					{
						throw new TorConnectionException($"Not connected to Tor SOCKS5 proxy: {TorSocks5EndPoint}.");
					}
					builder.Append(receiveBuffer[..receiveCount]);
				}

				return builder.ToArray();
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Send operation was canceled.");
				throw;
			}
			catch (IOException ex)
			{
				throw new TorConnectionException($"{nameof(TorConnection)} is not connected.", ex);
			}
		}
	}
}
