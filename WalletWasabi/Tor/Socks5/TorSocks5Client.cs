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
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Bases;
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
		private TcpClient TcpClient { get; set; }

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
					throw new TorConnectionException($"Could not connect to Tor SOCKSPort at {host}:{port}. Is Tor running?", ex);
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
			catch (TorConnectionException)
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

			byte[] receiveBuffer = await SendRequestAsync(new VersionMethodRequest(methods), cancellationToken).ConfigureAwait(false);

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
				var passwd = new PasswdField(password: identity);
				var usernamePasswordRequest = new UsernamePasswordRequest(uName, passwd);

				receiveBuffer = await SendRequestAsync(usernamePasswordRequest, cancellationToken).ConfigureAwait(false);

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
			await sslStream.AuthenticateAsClientAsync(host, new X509CertificateCollection(), true).ConfigureAwait(false);
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
				TorSocks5Request connectRequest = new(cmd: CmdField.Connect, new AddrField(host), new PortField(port));
				byte[] receiveBuffer = await SendRequestAsync(connectRequest, cancellationToken).ConfigureAwait(false);

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
					throw new TorConnectCommandFailedException(connectionResponse.Rep);
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
					throw new TorConnectionException($"{nameof(TorSocks5Client)} is not connected to '{RemoteEndPoint}'.", ex);
				}
				if (!IsConnected)
				{
					throw new TorConnectionException($"{nameof(TorSocks5Client)} is not connected to '{RemoteEndPoint}'.");
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
		/// Sends a request to the Tor SOCKS5 connection and returns a byte response.
		/// </summary>
		/// <param name="request">Request to send.</param>
		/// <param name="cancellationToken">Cancellation token to cancel sending.</param>
		/// <returns>Reply</returns>
		/// <exception cref="ArgumentException">When <paramref name="request"/> is not supported.</exception>
		/// <exception cref="TorConnectionException">When we receive no response from Tor or the response is invalid.</exception>
		private async Task<byte[]> SendRequestAsync(ByteArraySerializableBase request, CancellationToken cancellationToken = default)
		{
			try
			{
				await AssertConnectedAsync(cancellationToken).ConfigureAwait(false);

				using (await AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
				{
					var stream = TcpClient.GetStream();

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
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Send operation was canceled.");
				throw;
			}
			catch (IOException ex)
			{
				throw new TorConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.", ex);
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
			finally
			{
				TcpClient = null; // needs to be called, .net bug
			}
		}

		#endregion IDisposable Support
	}
}
