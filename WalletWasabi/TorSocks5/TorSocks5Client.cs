using Nito.AsyncEx;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;
using WalletWasabi.TorSocks5.Models.Messages;
using WalletWasabi.TorSocks5.Models.TorSocks5.Fields.ByteArrayFields;
using WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.TorSocks5
{
	/// <summary>
	/// Create an instance with the TorSocks5Manager
	/// </summary>
	public class TorSocks5Client : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		#region Constructors

		/// <param name="endPoint">Opt out Tor with null.</param>
		internal TorSocks5Client(EndPoint endPoint)
		{
			TorSocks5EndPoint = endPoint;
			TcpClient = endPoint is null ? new TcpClient() : new TcpClient(endPoint.AddressFamily);
			AsyncLock = new AsyncLock();
		}

		/// <param name="tcpClient">Must be already connected.</param>
		internal TorSocks5Client(TcpClient tcpClient)
		{
			Guard.NotNull(nameof(tcpClient), tcpClient);
			TcpClient = tcpClient;
			AsyncLock = new AsyncLock();
			Stream = tcpClient.GetStream();
			TorSocks5EndPoint = null;
			DestinationHost = tcpClient.Client.RemoteEndPoint.GetHostOrDefault();
			DestinationPort = tcpClient.Client.RemoteEndPoint.GetPortOrDefault().Value;
			RemoteEndPoint = tcpClient.Client.RemoteEndPoint;
			if (!IsConnected)
			{
				throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.");
			}
		}

		#endregion Constructors

		#region PropertiesAndMembers

		public TcpClient TcpClient { get; private set; }

		public EndPoint TorSocks5EndPoint { get; private set; }

		public Stream Stream { get; internal set; }

		public string DestinationHost { get; private set; }

		public int DestinationPort { get; private set; }

		private EndPoint RemoteEndPoint { get; set; }

		public bool IsConnected
		{
			get
			{
				try
				{
					return !(TcpClient is null) && TcpClient.Connected;
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
					return false;
				}
			}
		}

		internal AsyncLock AsyncLock { get; }

		#endregion PropertiesAndMembers

		#region Initializers

		internal async Task ConnectAsync()
		{
			if (TorSocks5EndPoint is null)
			{
				return;
			}

			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				string host = TorSocks5EndPoint.GetHostOrDefault();
				int? port = TorSocks5EndPoint.GetPortOrDefault();
				try
				{
					await TcpClient.ConnectAsync(host, port.Value).ConfigureAwait(false);
				}
				catch (Exception ex) when (IsConnectionRefused(ex))
				{
					throw new ConnectionException(
						$"Could not connect to Tor SOCKSPort at {host}:{port}. Is Tor running?", ex);
				}

				Stream = TcpClient.GetStream();
				RemoteEndPoint = TcpClient.Client.RemoteEndPoint;
			}
		}

		/// <summary>
		/// IsolateSOCKSAuth must be on (on by default)
		/// https://www.torproject.org/docs/tor-manual.html.en
		/// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n35
		/// </summary>
		internal async Task HandshakeAsync(bool isolateStream = true)
		{
			await HandshakeAsync(isolateStream ? RandomString.Generate(21) : "").ConfigureAwait(false);
		}

		/// <summary>
		/// IsolateSOCKSAuth must be on (on by default)
		/// https://www.torproject.org/docs/tor-manual.html.en
		/// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n35
		/// </summary>
		/// <param name="identity">Isolates streams by identity. If identity is empty string, it won't isolate stream.</param>
		internal async Task HandshakeAsync(string identity)
		{
			if (TorSocks5EndPoint is null)
			{
				return;
			}

			Guard.NotNull(nameof(identity), identity);

			MethodsField methods = string.IsNullOrWhiteSpace(identity)
				? new MethodsField(MethodField.NoAuthenticationRequired)
				: new MethodsField(MethodField.UsernamePassword);

			var sendBuffer = new VersionMethodRequest(methods).ToBytes();

			var receiveBuffer = await SendAsync(sendBuffer, 2).ConfigureAwait(false);

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
				// subnegotiation begins. This begins with the client producing a
				// Username / Password request:
				var username = identity;
				var password = identity;
				var uName = new UNameField(username);
				var passwd = new PasswdField(password);
				var usernamePasswordRequest = new UsernamePasswordRequest(uName, passwd);
				sendBuffer = usernamePasswordRequest.ToBytes();

				Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
				receiveBuffer = await SendAsync(sendBuffer, 2).ConfigureAwait(false);

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
		}

		internal async Task ConnectToDestinationAsync(EndPoint destination, bool isRecursiveCall = false)
		{
			Guard.NotNull(nameof(destination), destination);
			await ConnectToDestinationAsync(destination.GetHostOrDefault(), destination.GetPortOrDefault().Value, isRecursiveCall: isRecursiveCall).ConfigureAwait(false);
		}

		/// <param name="host">IPv4 or domain</param>
		internal async Task ConnectToDestinationAsync(string host, int port, bool isRecursiveCall = false)
		{
			host = Guard.NotNullOrEmptyOrWhitespace(nameof(host), host, true);
			Guard.MinimumAndNotNull(nameof(port), port, 0);

			if (TorSocks5EndPoint is null)
			{
				using (await AsyncLock.LockAsync().ConfigureAwait(false))
				{
					TcpClient?.Dispose();
					TcpClient = IPAddress.TryParse(host, out IPAddress ip) ? new TcpClient(ip.AddressFamily) : new TcpClient();
					await TcpClient.ConnectAsync(host, port).ConfigureAwait(false);
					Stream = TcpClient.GetStream();
					RemoteEndPoint = TcpClient.Client.RemoteEndPoint;
				}

				return;
			}

			var cmd = CmdField.Connect;

			var dstAddr = new AddrField(host);
			DestinationHost = dstAddr.DomainOrIPv4;

			var dstPort = new PortField(port);
			DestinationPort = dstPort.DstPort;

			var connectionRequest = new TorSocks5Request(cmd, dstAddr, dstPort);
			var sendBuffer = connectionRequest.ToBytes();

			var receiveBuffer = await SendAsync(sendBuffer, isRecursiveCall: isRecursiveCall).ConfigureAwait(false);

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

		public async Task AssertConnectedAsync(bool isRecursiveCall = false)
		{
			if (!IsConnected)
			{
				// try reconnect, maybe the server came online already
				try
				{
					await ConnectToDestinationAsync(RemoteEndPoint, isRecursiveCall: isRecursiveCall).ConfigureAwait(false);
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
			Exception error = null;
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

			return error != null;
		}

		/// <summary>
		/// Sends bytes to the Tor Socks5 connection
		/// </summary>
		/// <param name="sendBuffer">Sent data</param>
		/// <param name="receiveBufferSize">Maximum number of bytes expected to be received in the reply</param>
		/// <returns>Reply</returns>
		public async Task<byte[]> SendAsync(byte[] sendBuffer, int? receiveBufferSize = null, bool isRecursiveCall = false)
		{
			Guard.NotNullOrEmpty(nameof(sendBuffer), sendBuffer);

			try
			{
				if (!isRecursiveCall) // Because AssertConnectedAsync would be calling it again.
				{
					await AssertConnectedAsync(isRecursiveCall: true).ConfigureAwait(false);
				}

				using (await AsyncLock.LockAsync().ConfigureAwait(false))
				{
					var stream = TcpClient.GetStream();

					// Write data to the stream
					await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length).ConfigureAwait(false);
					await stream.FlushAsync().ConfigureAwait(false);

					// If receiveBufferSize is null, zero or negative or bigger than TcpClient.ReceiveBufferSize
					// then work with TcpClient.ReceiveBufferSize
					var tcpReceiveBuffSize = TcpClient.ReceiveBufferSize;
					var actualReceiveBufferSize = receiveBufferSize is null || receiveBufferSize <= 0 || receiveBufferSize > tcpReceiveBuffSize
						? tcpReceiveBuffSize
						: (int)receiveBufferSize;

					// Receive the response
					var receiveBuffer = new byte[actualReceiveBufferSize];

					int receiveCount = await stream.ReadAsync(receiveBuffer, 0, actualReceiveBufferSize).ConfigureAwait(false);

					if (receiveCount <= 0)
					{
						throw new ConnectionException($"Not connected to Tor SOCKS5 proxy: {TorSocks5EndPoint}.");
					}
					// if we could fit everything into our buffer, then return it
					if (!stream.DataAvailable)
					{
						return receiveBuffer[..receiveCount];
					}

					// while we have data available, start building a bytearray
					var builder = new ByteArrayBuilder();
					builder.Append(receiveBuffer[..receiveCount]);
					while (stream.DataAvailable)
					{
						Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
						receiveCount = await stream.ReadAsync(receiveBuffer, 0, actualReceiveBufferSize).ConfigureAwait(false);
						if (receiveCount <= 0)
						{
							throw new ConnectionException($"Not connected to Tor SOCKS5 proxy: {TorSocks5EndPoint}.");
						}
						builder.Append(receiveBuffer[..receiveCount]);
					}

					return builder.ToArray();
				}
			}
			catch (IOException ex)
			{
				if (isRecursiveCall)
				{
					throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.", ex);
				}
				else
				{
					// try reconnect, maybe the server came online already
					try
					{
						await ConnectToDestinationAsync(RemoteEndPoint, isRecursiveCall: true).ConfigureAwait(false);
					}
					catch (Exception ex2) when (IsConnectionRefused(ex2))
					{
						throw new ConnectionException($"{nameof(TorSocks5Client)} is not connected to {RemoteEndPoint}.", ex2);
					}
					return await SendAsync(sendBuffer, receiveBufferSize, isRecursiveCall: true).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		/// When Tor receives a "RESOLVE" SOCKS command, it initiates
		/// a remote lookup of the hostname provided as the target address in the SOCKS
		/// request.
		/// </summary>
		internal async Task<IPAddress> ResolveAsync(string host)
		{
			// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n44

			host = Guard.NotNullOrEmptyOrWhitespace(nameof(host), host, true);

			if (TorSocks5EndPoint is null)
			{
				var hostAddresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
				return hostAddresses.First();
			}

			var cmd = CmdField.Resolve;

			var dstAddr = new AddrField(host);

			var dstPort = new PortField(0);

			var resolveRequest = new TorSocks5Request(cmd, dstAddr, dstPort);
			var sendBuffer = resolveRequest.ToBytes();

			var receiveBuffer = await SendAsync(sendBuffer).ConfigureAwait(false);

			var resolveResponse = new TorSocks5Response();
			resolveResponse.FromBytes(receiveBuffer);

			if (resolveResponse.Rep != RepField.Succeeded)
			{
				throw new TorSocks5FailureResponseException(resolveResponse.Rep);
			}
			return IPAddress.Parse(resolveResponse.BndAddr.DomainOrIPv4);
		}

		/// <summary>
		/// Tor attempts to find the canonical hostname for that IPv4 record
		/// </summary>
		internal async Task<string> ReverseResolveAsync(IPAddress iPv4)
		{
			// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n55

			Guard.NotNull(nameof(iPv4), iPv4);

			if (TorSocks5EndPoint is null) // Only Tor is iPv4 dependent
			{
				var host = await Dns.GetHostEntryAsync(iPv4).ConfigureAwait(false);
				return host.HostName;
			}

			Guard.Same($"{nameof(iPv4)}.{nameof(iPv4.AddressFamily)}", AddressFamily.InterNetwork, iPv4.AddressFamily);

			var cmd = CmdField.ResolvePtr;

			var dstAddr = new AddrField(iPv4.ToString());

			var dstPort = new PortField(0);

			var resolveRequest = new TorSocks5Request(cmd, dstAddr, dstPort);
			var sendBuffer = resolveRequest.ToBytes();

			var receiveBuffer = await SendAsync(sendBuffer).ConfigureAwait(false);

			var resolveResponse = new TorSocks5Response();
			resolveResponse.FromBytes(receiveBuffer);

			if (resolveResponse.Rep != RepField.Succeeded)
			{
				throw new TorSocks5FailureResponseException(resolveResponse.Rep);
			}
			return resolveResponse.BndAddr.DomainOrIPv4;
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
				if (TcpClient != null)
				{
					if (TcpClient.Connected)
					{
						Stream?.Dispose();
					}
					TcpClient?.Dispose();
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
