using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// TCP connection to Tor SOCKS5 endpoint.
	/// </summary>
	public class TorConnection : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public TorConnection(TcpClient tcpClient, Stream transportStream)
		{
			TcpClient = tcpClient;
			TransportStream = transportStream;
		}

		/// <summary>TCP connection to Tor's SOCKS5 server.</summary>
		private TcpClient TcpClient { get; }

		/// <summary>Transport stream for sending  HTTP/HTTPS requests through Tor's SOCKS5 server.</summary>
		/// <remarks>This stream is not to be used to send commands to Tor's SOCKS5 server.</remarks>
		private Stream TransportStream { get; }

		public bool IsConnected => TcpClient.Connected is true;

		/// <summary>
		/// Stream to transport HTTP(s) request.
		/// </summary>
		/// <remarks>Either <see cref="TcpClient.GetStream"/> or <see cref="SslStream"/> over <see cref="TcpClient.GetStream"/>.</remarks>
		public Stream GetTransportStream()
		{
			return TransportStream;
		}

		/// <summary>
		/// <list type="bullet">
		/// <item>Unmanaged resources need to be released regardless of the value of the <paramref name="disposing"/> parameter.</item>
		/// <item>Managed resources need to be released if the value of <paramref name="disposing"/> is <c>true</c>.</item>
		/// </list>
		/// </summary>
		/// <param name="disposing">
		/// Indicates whether the method call comes from a <see cref="Dispose()"/> method
		/// (its value is <c>true</c>) or from a finalizer (its value is <c>false</c>).
		/// </param>
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
		/// Do not change this code.
		/// </summary>
		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);
			// Suppress finalization.
			GC.SuppressFinalize(this);
		}
	}
}