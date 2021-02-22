using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// Wraps a TCP connection to Tor SOCKS5 endpoint.
	/// </summary>
	public class TorTcpConnection : IDisposable
	{
		private volatile bool _disposedValue = false;

		public TorTcpConnection(TcpClient tcpClient, Stream transportStream)
		{
			TcpClient = tcpClient;
			TransportStream = transportStream;
		}

		/// <summary>TCP connection to Tor's SOCKS5 server.</summary>
		private TcpClient TcpClient { get; }

		/// <summary>Transport stream for sending  HTTP/HTTPS requests through Tor's SOCKS5 server.</summary>
		/// <remarks>This stream is not to be used to send commands to Tor's SOCKS5 server.</remarks>
		private Stream TransportStream { get; }

		/// <summary>
		/// Stream to transport HTTP(s) request.
		/// </summary>
		/// <remarks>Either <see cref="TcpClient.GetStream"/> or <see cref="SslStream"/> over <see cref="TcpClient.GetStream"/>.</remarks>
		public Stream GetTransportStream() => TransportStream;

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					TcpClient.Dispose();
				}
				_disposedValue = true;
			}
		}

		/// <summary>
		/// This code added to correctly implement the disposable pattern.
		/// </summary>
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}
	}
}
