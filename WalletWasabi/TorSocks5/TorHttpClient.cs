using Nito.AsyncEx;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Http.Models;
using WalletWasabi.Logging;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;

namespace WalletWasabi.TorSocks5
{
	public class TorHttpClient : IDisposable
	{
		private static DateTimeOffset? TorDoesntWorkSinceBacking = null;

		public static DateTimeOffset? TorDoesntWorkSince
		{
			get => TorDoesntWorkSinceBacking;
			private set
			{
				if (value != TorDoesntWorkSinceBacking)
				{
					TorDoesntWorkSinceBacking = value;
					if (value is null)
					{
						LatestTorException = null;
					}
				}
			}
		}

		public static Exception LatestTorException { get; private set; } = null;

		public Uri DestinationUri => DestinationUriAction();
		public Func<Uri> DestinationUriAction { get; private set; }
		public EndPoint TorSocks5EndPoint { get; private set; }
		public bool IsTorUsed => TorSocks5EndPoint != null;

		public bool IsolateStream { get; private set; }

		public TorSocks5Client TorSocks5Client { get; private set; }

		private static AsyncLock AsyncLock { get; } = new AsyncLock(); // We make everything synchronous, so slow, but at least stable.

		public TorHttpClient(Uri baseUri, EndPoint torSocks5EndPoint, bool isolateStream = false)
		{
			baseUri = Guard.NotNull(nameof(baseUri), baseUri);
			Create(torSocks5EndPoint, isolateStream, () => baseUri);
		}

		public TorHttpClient(Func<Uri> baseUriAction, EndPoint torSocks5EndPoint, bool isolateStream = false)
		{
			Create(torSocks5EndPoint, isolateStream, baseUriAction);
		}

		private void Create(EndPoint torSocks5EndPoint, bool isolateStream, Func<Uri> baseUriAction)
		{
			DestinationUriAction = Guard.NotNull(nameof(baseUriAction), baseUriAction);
			TorSocks5EndPoint = DestinationUri.IsLoopback ? null : torSocks5EndPoint;
			TorSocks5Client = null;
			IsolateStream = isolateStream;
		}

		/// <remarks>
		/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null, CancellationToken cancel = default)
		{
			Guard.NotNull(nameof(method), method);
			relativeUri = Guard.NotNull(nameof(relativeUri), relativeUri);
			var requestUri = new Uri(DestinationUri, relativeUri);
			var request = new HttpRequestMessage(method, requestUri);
			if (content != null)
			{
				request.Content = content;
			}
			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

			try
			{
				using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
				{
					try
					{
						HttpResponseMessage ret = await SendAsync(request).ConfigureAwait(false);
						TorDoesntWorkSince = null;
						return ret;
					}
					catch (Exception ex)
					{
						Logger.LogTrace(ex);

						TorSocks5Client?.Dispose(); // rebuild the connection and retry
						TorSocks5Client = null;

						cancel.ThrowIfCancellationRequested();
						try
						{
							HttpResponseMessage ret2 = await SendAsync(request).ConfigureAwait(false);
							TorDoesntWorkSince = null;
							return ret2;
						}
						// If we get ttlexpired then wait and retry again linux often do this.
						catch (TorSocks5FailureResponseException ex2) when (ex2.RepField == RepField.TtlExpired)
						{
							Logger.LogTrace(ex);

							TorSocks5Client?.Dispose(); // rebuild the connection and retry
							TorSocks5Client = null;

							try
							{
								await Task.Delay(1000, cancel).ConfigureAwait(false);
							}
							catch (TaskCanceledException tce)
							{
								throw new OperationCanceledException(tce.Message, tce, cancel);
							}
						}
						catch (SocketException ex3) when (ex3.ErrorCode == (int)SocketError.ConnectionRefused)
						{
							throw new ConnectionException("Connection was refused.", ex3);
						}

						cancel.ThrowIfCancellationRequested();

						HttpResponseMessage ret3 = await SendAsync(request).ConfigureAwait(false);
						TorDoesntWorkSince = null;
						return ret3;
					}
				}
			}
			catch (TaskCanceledException ex)
			{
				SetTorNotWorkingState(ex);
				throw new OperationCanceledException(ex.Message, ex, cancel);
			}
			catch (Exception ex)
			{
				SetTorNotWorkingState(ex);
				throw;
			}
		}

		private static void SetTorNotWorkingState(Exception ex)
		{
			if (TorDoesntWorkSince is null)
			{
				TorDoesntWorkSince = DateTimeOffset.UtcNow;
			}
			LatestTorException = ex;
		}

		/// <remarks>
		/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
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

			if (TorSocks5Client is null || !TorSocks5Client.IsConnected)
			{
				TorSocks5Client = new TorSocks5Client(TorSocks5EndPoint);
				await TorSocks5Client.ConnectAsync().ConfigureAwait(false);
				await TorSocks5Client.HandshakeAsync(IsolateStream).ConfigureAwait(false);
				await TorSocks5Client.ConnectToDestinationAsync(host, request.RequestUri.Port).ConfigureAwait(false);

				Stream stream = TorSocks5Client.TcpClient.GetStream();
				if (request.RequestUri.Scheme == "https")
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
							checkCertificateRevocation: true).ConfigureAwait(false);
					stream = sslStream;
				}

				TorSocks5Client.Stream = stream;
			}
			cancel.ThrowIfCancellationRequested();

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
					if (request.Content is null)
					{
						request.Content = new ByteArrayContent(new byte[] { }); // dummy empty content
						request.Content.Headers.ContentLength = 0;
					}
					else
					{
						if (request.Content.Headers.ContentLength is null)
						{
							request.Content.Headers.ContentLength = (await request.Content.ReadAsStringAsync().ConfigureAwait(false)).Length;
						}
					}
				}
			}

			var requestString = await request.ToHttpStringAsync().ConfigureAwait(false);

			var bytes = Encoding.UTF8.GetBytes(requestString);

			await TorSocks5Client.Stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
			await TorSocks5Client.Stream.FlushAsync().ConfigureAwait(false);
			using (var httpResponseMessage = new HttpResponseMessage())
			{
				return await HttpResponseMessageExtensions.CreateNewAsync(TorSocks5Client.Stream, request.Method).ConfigureAwait(false);
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					TorSocks5Client?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// ~TorHttpClient() {
		// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
