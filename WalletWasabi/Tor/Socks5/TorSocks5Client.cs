using Nito.AsyncEx;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Exceptions;
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

		public TcpClient TcpClient { get; private set; }

		private EndPoint TorSocks5EndPoint { get; }

		public Stream Stream { get; internal set; }

		private EndPoint RemoteEndPoint { get; set; }

		public bool IsConnected => TcpClient?.Connected is true;

		internal AsyncLock AsyncLock { get; }

		#endregion PropertiesAndMembers

		#region Initializers

		internal async Task ConnectAsync()
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
				await client.HandshakeAsync(isolateStream: true).ConfigureAwait(false);

				return true;
			}
			catch (ConnectionException)
			{
				return false;
			}
		}

		/// <summary>
		/// IsolateSOCKSAuth must be on (on by default)
		/// https://www.torproject.org/docs/tor-manual.html.en
		/// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n35
		/// </summary>
		internal async Task HandshakeAsync(bool isolateStream = true, CancellationToken cancellationToken = default)
		{
			string identity = isolateStream ? RandomString.CapitalAlphaNumeric(21) : "";
			await HandshakeAsync(identity, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// IsolateSOCKSAuth must be on (on by default)
		/// https://www.torproject.org/docs/tor-manual.html.en
		/// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n35
		/// </summary>
		/// <param name="identity">Isolates streams by identity. If identity is empty string, it won't isolate stream.</param>
		private async Task HandshakeAsync(string identity, CancellationToken cancellationToken = default)
		{
			Logger.LogDebug($"> {nameof(identity)}={identity}");

			MethodsField methods = string.IsNullOrWhiteSpace(identity)
				? new MethodsField(MethodField.NoAuthenticationRequired)
				: new MethodsField(MethodField.UsernamePassword);

			byte[] sendBuffer = new VersionMethodRequest(methods).ToBytes();
			byte[] receiveBuffer = await SendAsync(sendBuffer, receiveBufferSize: 2, cancellationToken).ConfigureAwait(false);

			var methodSelection = new MethodSelectionResponse();
			methodSelection.FromBytes(receiveBuffer);

			if (methodSelection.Ver != VerField.Socks5)
			{
				throw new NotSupportedException($"SOCKS{methodSelection.Ver.Value} not supported. Only SOCKS5 is supported.");
			}

			if (methodSelection.Method == MethodField.NoAcceptableMethods)
			{
				// https://www.ietf.org/rfc/rfc1928.txt
				// If the selected METHOD is X'FF', none of the methods listed by the
				// client are acceptable, and the client MUST close the connection.
				DisposeTcpClient();
				throw new NotSupportedException("Tor's SOCKS5 proxy does not support any of the client's authentication methods.");
			}

			if (methodSelection.Method == MethodField.UsernamePassword)
			{
				// https://tools.ietf.org/html/rfc1929#section-2
				// Once the SOCKS V5 server has started, and the client has selected the
				// Username / Password Authentication protocol, the Username / Password
				// sub-negotiation begins. This begins with the client producing a
				// Username / Password request:
				var uName = new UNameField(uName: identity);
				var passwd = new PasswdField(passwd: identity);
				var usernamePasswordRequest = new UsernamePasswordRequest(uName, passwd);
				sendBuffer = usernamePasswordRequest.ToBytes();

				Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
				receiveBuffer = await SendAsync(sendBuffer, receiveBufferSize: 2, cancellationToken).ConfigureAwait(false);

				var userNamePasswordResponse = new UsernamePasswordResponse();
				userNamePasswordResponse.FromBytes(receiveBuffer);
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

		internal async Task ConnectToDestinationAsync(EndPoint destination, CancellationToken cancellationToken = default)
		{
			if (!destination.TryGetHostAndPort(out string? host, out int? port))
			{
				throw new ArgumentException("Endpoint type is not supported.", nameof(destination));
			}

			await ConnectToDestinationAsync(host, port.Value, cancellationToken).ConfigureAwait(false);
		}

		/// <param name="host">IPv4 or domain</param>
		internal async Task ConnectToDestinationAsync(string host, int port, CancellationToken cancellationToken = default)
		{
			Logger.LogDebug($"> {nameof(host)}={host}, {nameof(port)}={port}");

			host = Guard.NotNullOrEmptyOrWhitespace(nameof(host), host, true);
			Guard.MinimumAndNotNull(nameof(port), port, 0);

			try
			{
				var connectionRequest = new TorSocks5Request(cmd: CmdField.Connect, new AddrField(host), new PortField(port));
				var sendBuffer = connectionRequest.ToBytes();

				var receiveBuffer = await SendAsync(sendBuffer, receiveBufferSize: null, cancellationToken).ConfigureAwait(false);

				var connectionResponse = new TorSocks5Response();
				connectionResponse.FromBytes(receiveBuffer);

				if (connectionResponse.Rep != RepField.Succeeded)
				{
					// https://www.ietf.org/rfc/rfc1928.txt
					// When a reply(REP value other than X'00') indicates a failure, the
					// SOCKS server MUST terminate the TCP connection shortly after sending
					// the reply.This must be no more than 10 seconds after detecting the
					// condition that caused a failure.
					DisposeTcpClient();
					Logger.LogWarning($"Connection response indicates a failure. Actual response is: '{connectionResponse.Rep}'.");
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
				Logger.LogTrace($"Connecting to destination {host}:{port} was canceled.");
				throw;
			}
			catch (Exception e)
			{
				Logger.LogError("Exception was thrown when connecting to destination.", e);
				throw;
			}
			finally
			{
				Logger.LogDebug("<");
			}
		}

		public async Task AssertConnectedAsync()
		{
			if (!IsConnected)
			{
				// try reconnect, maybe the server came online already
				try
				{
					await ConnectToDestinationAsync(RemoteEndPoint).ConfigureAwait(false);
				}
				catch (Exception ex) when (IsConnectionRefused(ex))
				{
					throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.", ex);
				}
				if (!IsConnected)
				{
					throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.");
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
		public async Task<byte[]> SendAsync(byte[] sendBuffer, int? receiveBufferSize = null, CancellationToken cancellationToken = default)
		{
			Guard.NotNullOrEmpty(nameof(sendBuffer), sendBuffer);

			try
			{
				await AssertConnectedAsync().ConfigureAwait(false);

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
			finally
			{
				TcpClient = null; // needs to be called, .net bug
			}
		}

		#endregion IDisposable Support
	}
}
