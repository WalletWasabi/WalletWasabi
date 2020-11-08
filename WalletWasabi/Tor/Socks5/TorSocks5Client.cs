using Nito.AsyncEx;
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
using WalletWasabi.Tor.Exceptions;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Models.Messages;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// Create an instance with the TorSocks5Manager
	/// </summary>
	public class TorSocks5Client : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		/// <param name="endPoint">Valid Tor end point.</param>
		public TorSocks5Client(EndPoint endPoint)
		{
			TorSocks5EndPoint = endPoint;
			TcpClient = new TcpClient(endPoint.AddressFamily);
			AsyncLock = new AsyncLock();
		}

		#region PropertiesAndMembers

		/// <summary>TCP connection to Tor's SOCKS5 server.</summary>
		private TcpClient TcpClient { get; }

		private EndPoint TorSocks5EndPoint { get; }

		/// <summary>Transport stream for sending  HTTP/HTTPS requests through Tor's SOCKS5 server.</summary>
		/// <remarks>This stream is not to be used to send commands to Tor's SOCKS5 server.</remarks>
		private Stream Stream { get; set; }

		private EndPoint RemoteEndPoint { get; set; }

		public bool IsConnected => TcpClient?.Connected is true;

		internal AsyncLock AsyncLock { get; }

		#endregion PropertiesAndMembers

		#region Initializers

		/// <summary>
		/// Establishes TCP connection with Tor's SOCKS5 server.
		/// </summary>
		public async Task ConnectAsync()
		{
			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				if (!TorSocks5EndPoint.TryGetHostAndPort(out string? host, out int? port))
				{
					throw new ArgumentException("Endpoint type is not supported.", nameof(TorSocks5EndPoint));
				}

				try
				{
					// Cancellation token for ConnectAsync will be available in .NET 5.
					await TcpClient.ConnectAsync(host, port.Value).ConfigureAwait(false);
				}
				catch (Exception ex) when (IsConnectionRefused(ex))
				{
					throw new ConnectionException($"Could not connect to Tor SOCKSPort at {host}:{port}. Is Tor running?", ex);
				}

				Stream = TcpClient.GetStream();
				RemoteEndPoint = TcpClient.Client.RemoteEndPoint;
			}
		}

		/// <summary>
		/// Checks whether communication can be established with Tor over <see cref="TorSocks5EndPoint"/> endpoint.
		/// </summary>
		/// <returns></returns>
		public async Task<bool> IsTorRunningAsync()
		{
			try
			{
				// Internal TCP client may close, so we need a new instance here.
				using var client = new TorSocks5Client(TorSocks5EndPoint);
				await client.ConnectAsync().ConfigureAwait(false);
				await client.HandshakeAsync().ConfigureAwait(false);

				return true;
			}
			catch (ConnectionException)
			{
				return false;
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
		public async Task HandshakeAsync(bool isolateStream = false, CancellationToken cancellationToken = default)
		{
			Logger.LogDebug($"> {nameof(isolateStream)}={isolateStream}");

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
			byte[] receiveBuffer = await SendAsync(sendBuffer, receiveBufferSize: 2, cancellationToken).ConfigureAwait(false);

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
				DisposeTcpClient();
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
				receiveBuffer = await SendAsync(sendBuffer, receiveBufferSize: 2, cancellationToken).ConfigureAwait(false);

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
					DisposeTcpClient();
					throw new InvalidOperationException("Wrong username and/or password.");
				}
			}

			Logger.LogDebug("<");
		}

		public async Task UpgradeToSslAsync(string host)
		{
			SslStream sslStream = new SslStream(TcpClient.GetStream(), leaveInnerStreamOpen: true);
			await sslStream.AuthenticateAsClientAsync(host, new X509CertificateCollection(), IHttpClient.SupportedSslProtocols, true).ConfigureAwait(false);
			Stream = sslStream;
		}

		/// <summary>
		/// Stream to transport HTTP(S) request.
		/// </summary>
		/// <remarks>Either <see cref="TcpClient.GetStream"/> or <see cref="SslStream"/> over <see cref="TcpClient.GetStream"/>.</remarks>
		public Stream GetTransportStream()
		{
			return Stream;
		}

		private async Task ConnectToDestinationAsync(EndPoint destination, CancellationToken cancellationToken = default)
		{
			if (!destination.TryGetHostAndPort(out string? host, out int? port))
			{
				throw new ArgumentException("Endpoint type is not supported.", nameof(destination));
			}

			await ConnectToDestinationAsync(host, port.Value, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Sends <see cref="CmdField.Connect"/> command to SOCKS5 server to instruct it to connect to
		/// <paramref name="host"/>:<paramref name="port"/> on behalf of this client.
		/// </summary>
		/// <param name="host">IPv4 or domain of the destination.</param>
		/// <param name="port">Port number of the destination.</param>
		/// <seealso href="https://tools.ietf.org/html/rfc1928">Section 3. Procedure for TCP-based clients</seealso>
		public async Task ConnectToDestinationAsync(string host, int port, CancellationToken cancellationToken = default)
		{
			Logger.LogDebug($"> {nameof(host)}='{host}', {nameof(port)}={port}");

			host = Guard.NotNullOrEmptyOrWhitespace(nameof(host), host, true);
			Guard.MinimumAndNotNull(nameof(port), port, 0);

			try
			{
				var connectionRequest = new TorSocks5Request(cmd: CmdField.Connect, new AddrField(host), new PortField(port));
				var sendBuffer = connectionRequest.ToBytes();

				var receiveBuffer = await SendAsync(sendBuffer, receiveBufferSize: null, cancellationToken).ConfigureAwait(false);

				var connectionResponse = new TorSocks5Response(receiveBuffer);

				if (connectionResponse.Rep != RepField.Succeeded)
				{
					// https://www.ietf.org/rfc/rfc1928.txt
					// When a reply (REP value other than X'00') indicates a failure, the
					// SOCKS server MUST terminate the TCP connection shortly after sending
					// the reply. This must be no more than 10 seconds after detecting the
					// condition that caused a failure.
					DisposeTcpClient();
					Logger.LogWarning($"Connection response indicates a failure. Actual response is: '{connectionResponse.Rep}'. Request: '{host}:{port}'.");
					throw new TorSocks5FailureResponseException(connectionResponse.Rep);
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
			catch (Exception e)
			{
				Logger.LogError($"Exception was thrown when connecting to destination '{host}:{port}'.", e);
				throw;
			}
			finally
			{
				Logger.LogDebug("<");
			}
		}

		private async Task AssertConnectedAsync(CancellationToken token = default)
		{
			if (!IsConnected)
			{
				// try reconnect, maybe the server came online already
				try
				{
					await ConnectToDestinationAsync(RemoteEndPoint, token).ConfigureAwait(false);
				}
				catch (Exception ex) when (IsConnectionRefused(ex))
				{
					throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to '{RemoteEndPoint}'.", ex);
				}
				if (!IsConnected)
				{
					throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to '{RemoteEndPoint}'.");
				}
			}
		}

		#endregion Initializers

		#region Methods

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
		/// Sends bytes to the Tor Socks5 connection
		/// </summary>
		/// <param name="sendBuffer">Sent data</param>
		/// <param name="receiveBufferSize">Maximum number of bytes expected to be received in the reply</param>
		/// <param name="cancellationToken">Cancellation token to cancel sending.</param>
		/// <returns>Reply</returns>
		private async Task<byte[]> SendAsync(byte[] sendBuffer, int? receiveBufferSize = null, CancellationToken cancellationToken = default)
		{
			Guard.NotNullOrEmpty(nameof(sendBuffer), sendBuffer);

			try
			{
				await AssertConnectedAsync(cancellationToken).ConfigureAwait(false);

				using (await AsyncLock.LockAsync().ConfigureAwait(false))
				{
					var stream = TcpClient.GetStream();

					// Write data to the stream
					await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length, cancellationToken).ConfigureAwait(false);
					await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

					// If receiveBufferSize is null, zero or negative or bigger than TcpClient.ReceiveBufferSize
					// then work with TcpClient.ReceiveBufferSize
					var tcpReceiveBuffSize = TcpClient.ReceiveBufferSize;
					var actualReceiveBufferSize = receiveBufferSize is null || receiveBufferSize <= 0 || receiveBufferSize > tcpReceiveBuffSize
						? tcpReceiveBuffSize
						: (int)receiveBufferSize;

					// Receive the response
					var receiveBuffer = new byte[actualReceiveBufferSize];

					int receiveCount = await stream.ReadAsync(receiveBuffer, 0, actualReceiveBufferSize, cancellationToken).ConfigureAwait(false);

					if (receiveCount <= 0)
					{
						throw new ConnectionException($"Not connected to Tor SOCKS5 proxy: {TorSocks5EndPoint}.");
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
							throw new ConnectionException($"Not connected to Tor SOCKS5 proxy: {TorSocks5EndPoint}.");
						}
						builder.Append(receiveBuffer[..receiveCount]);
					}

					return builder.ToArray();
				}
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace($"Send operation was canceled.");
				throw;
			}
			catch (IOException ex)
			{
				throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.", ex);
			}
		}

		#endregion Methods

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					DisposeTcpClient();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		private void DisposeTcpClient()
		{
			try
			{
				if (TcpClient is { } tcpClient)
				{
					if (tcpClient.Connected)
					{
						Stream?.Dispose();
					}
					tcpClient.Dispose();
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		#endregion IDisposable Support
	}
}
