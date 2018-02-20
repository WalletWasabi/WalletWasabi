using MagicalCryptoWallet.Exceptions;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Http.Models;
using MagicalCryptoWallet.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.TorSocks5
{
	public class TorHttpClient : IDisposable
	{
		public Uri DestinationUri { get; }
		public IPEndPoint TorSocks5EndPoint { get; }

		public bool IsolateStream { get; }

		public TorSocks5Client TorSocks5Client { get; private set; }

		private static AsyncLock AsyncLock { get; } = new AsyncLock(); // We make everything syncronous, so slow, but at least stable

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		public TorHttpClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null, bool isolateStream = false)
		{
			DestinationUri = Guard.NotNull(nameof(baseUri), baseUri);
			TorSocks5EndPoint = torSocks5EndPoint ?? new IPEndPoint(IPAddress.Loopback, 9050);
			TorSocks5Client = null;
			IsolateStream = isolateStream;
		}

		public static async Task<bool> IsTorRunningAsync(IPEndPoint torSocks5EndPoint = null)
		{
			using (var client = new TorSocks5Client(torSocks5EndPoint))
			{
				try
				{
					await client.ConnectAsync();
					await client.HandshakeAsync();
				}
				catch(ConnectionException)
				{
					return false;
				}
				return true;
			}
		}

		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null)
		{
			Guard.NotNull(nameof(method), method);
			relativeUri = Guard.NotNull(nameof(relativeUri), relativeUri);
			var requestUri = new Uri(DestinationUri, relativeUri);
			var request = new HttpRequestMessage(method, requestUri);
			if (content != null)
			{
				request.Content = content;
			}

			using (await AsyncLock.LockAsync())
			{
				try
				{
					return await SendAsync(request);
				}
				catch (Exception ex)
				{
					Logger.LogDebug<TorHttpClient>(ex);

					TorSocks5Client?.Dispose(); // rebuild the connection and retry
					TorSocks5Client = null;

					return await SendAsync(request);
				}
			}
		}

		private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
		{
			Guard.NotNull(nameof(request), request);

			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			var host = Guard.NotNullOrEmptyOrWhitespace($"{nameof(request)}.{nameof(request.RequestUri)}.{nameof(request.RequestUri.DnsSafeHost)}", request.RequestUri.DnsSafeHost, trim: true);


			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries
			// other than those acting as tunnels) MUST send their own HTTP - version
			// in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;

			if (TorSocks5Client != null && !TorSocks5Client.IsConnected)
			{
				TorSocks5Client?.Dispose();
				TorSocks5Client = null;
			}

			if (TorSocks5Client == null || !TorSocks5Client.IsConnected)
			{
				TorSocks5Client = new TorSocks5Client(TorSocks5EndPoint);
				await TorSocks5Client.ConnectAsync();
				await TorSocks5Client.HandshakeAsync(IsolateStream);
				await TorSocks5Client.ConnectToDestinationAsync(host, request.RequestUri.Port);

				Stream stream = TorSocks5Client.TcpClient.GetStream();
				if (request.RequestUri.Scheme.Equals("https", StringComparison.Ordinal))
				{
					SslStream sslStream;
					// On Linux and OSX ignore certificate, because of a .NET Core bug
					// This is a security vulnerability, has to be fixed as soon as the bug get fixed
					// Details:
					// https://github.com/dotnet/corefx/issues/21761
					// https://github.com/nopara73/DotNetTor/issues/4
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						sslStream = new SslStream(
							stream,
							leaveInnerStreamOpen: true);
					}
					else
					{
						sslStream = new SslStream(
							stream,
							leaveInnerStreamOpen: true,
							userCertificateValidationCallback: (a, b, c, d) => true);
					}

					await sslStream
						.AuthenticateAsClientAsync(
							host,
							new X509CertificateCollection(),
							SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
							checkCertificateRevocation: true);
					stream = sslStream;
				}

				TorSocks5Client.Stream = stream;
			}

			// https://tools.ietf.org/html/rfc7230#section-3.3.2
			// A user agent SHOULD send a Content - Length in a request message when
			// no Transfer-Encoding is sent and the request method defines a meaning
			// for an enclosed payload body.For example, a Content - Length header
			// field is normally sent in a POST request even when the value is 0
			// (indicating an empty payload body).A user agent SHOULD NOT send a
			// Content - Length header field when the request message does not contain
			// a payload body and the method semantics do not anticipate such a
			// body.
			if (request.Method == HttpMethod.Post)
			{
				if (request.Headers.TransferEncoding.Count == 0)
				{
					if (request.Content == null)
					{
						request.Content = new ByteArrayContent(new byte[] { }); // dummy empty content
						request.Content.Headers.ContentLength = 0;
					}
					else
					{
						if (request.Content.Headers.ContentLength == null)
						{
							request.Content.Headers.ContentLength = (await request.Content.ReadAsStringAsync()).Length;
						}
					}
				}
			}

			var requestString = await request.ToHttpStringAsync();

			var bytes = Encoding.UTF8.GetBytes(requestString);

			await TorSocks5Client.Stream.WriteAsync(bytes, 0, bytes.Length);
			await TorSocks5Client.Stream.FlushAsync();

			return await new HttpResponseMessage().CreateNewAsync(TorSocks5Client.Stream, request.Method);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
					TorSocks5Client?.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~TorHttpClient() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
