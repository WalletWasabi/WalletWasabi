using Nito.AsyncEx;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Http.Models;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : IRelativeHttpClient, IDisposable
	{
		private static DateTimeOffset? TorDoesntWorkSinceBacking = null;

		private volatile bool _disposedValue = false; // To detect redundant calls

		public TorHttpClient(Uri baseUri, EndPoint? torSocks5EndPoint, bool isolateStream = false) :
			this(() => baseUri, torSocks5EndPoint, isolateStream)
		{
			baseUri = Guard.NotNull(nameof(baseUri), baseUri);
		}

		public TorHttpClient(Func<Uri> baseUriAction, EndPoint? torSocks5EndPoint, bool isolateStream = false)
		{
			DestinationUriAction = Guard.NotNull(nameof(baseUriAction), baseUriAction);

			// Connecting to loopback's URIs cannot be done via Tor.
			TorSocks5EndPoint = DestinationUriAction().IsLoopback ? null : torSocks5EndPoint;
			TorSocks5Client = null;
			IsolateStream = isolateStream;
		}

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

		public static Exception? LatestTorException { get; private set; } = null;

		public Func<Uri> DestinationUriAction { get; }
		public EndPoint? TorSocks5EndPoint { get; private set; }

		private bool IsolateStream { get; }

		private TorSocks5Client? TorSocks5Client { get; set; }

		private static AsyncLock AsyncLock { get; } = new AsyncLock(); // We make everything synchronous, so slow, but at least stable.

		private Task<HttpResponseMessage> ClearnetRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			return new ClearnetHttpClient(DestinationUriAction).SendAsync(request, cancellationToken);
		}

		/// <remarks>
		/// Throws <see cref="OperationCanceledException"/> if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			Guard.NotNull(nameof(method), method);
			relativeUri = Guard.NotNull(nameof(relativeUri), relativeUri);
			var requestUri = new Uri(DestinationUriAction(), relativeUri);
			using var request = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				request.Content = content;
			}

			// Use clearnet HTTP client when Tor is disabled.
			if (TorSocks5EndPoint is null)
			{
				return await ClearnetRequestAsync(request, cancel).ConfigureAwait(false);
			}
			else
			{
				return await TorRequestAsync(request, cancel).ConfigureAwait(false);
			}
		}

		private async Task<HttpResponseMessage> TorRequestAsync(HttpRequestMessage request, CancellationToken cancel)
		{
			try
			{
				using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
				{
					try
					{
						HttpResponseMessage ret = await SendAsync(request, cancel).ConfigureAwait(false);
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
							HttpResponseMessage ret2 = await SendAsync(request, cancel).ConfigureAwait(false);
							TorDoesntWorkSince = null;
							return ret2;
						}
						// If we get ttlexpired then wait and retry again linux often do this.
						catch (TorConnectCommandFailedException ex2) when (ex2.RepField == RepField.TtlExpired)
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
							throw new TorConnectionException("Connection was refused.", ex3);
						}

						cancel.ThrowIfCancellationRequested();

						HttpResponseMessage ret3 = await SendAsync(request, cancel).ConfigureAwait(false);
						TorDoesntWorkSince = null;
						return ret3;
					}
				}
			}
			catch (TaskCanceledException ex)
			{
				SetTorNotWorkingState(ex);
				throw;
			}
			catch (Exception ex)
			{
				SetTorNotWorkingState(ex);
				throw;
			}
		}

		private static void SetTorNotWorkingState(Exception ex)
		{
			TorDoesntWorkSince ??= DateTimeOffset.UtcNow;
			LatestTorException = ex;
		}

		/// <exception cref="OperationCanceledException">If <paramref name="cancel"/> is set.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			// Use clearnet HTTP client when Tor is disabled.
			if (TorSocks5EndPoint is null)
			{
				return await ClearnetRequestAsync(request, cancel).ConfigureAwait(false);
			}
			else
			{
				return await TorRequestCoreAsync(request, cancel).ConfigureAwait(false);
			}
		}

		private async Task<HttpResponseMessage> TorRequestCoreAsync(HttpRequestMessage request, CancellationToken cancel)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			string host = Guard.NotNullOrEmptyOrWhitespace($"{nameof(request)}.{nameof(request.RequestUri)}.{nameof(request.RequestUri.DnsSafeHost)}", request.RequestUri.DnsSafeHost, trim: true);

			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries
			// other than those acting as tunnels) MUST send their own HTTP - version
			// in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;
			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

			if (TorSocks5Client is { } && !TorSocks5Client.IsConnected)
			{
				TorSocks5Client?.Dispose();
				TorSocks5Client = null;
			}

			if (TorSocks5Client is null || !TorSocks5Client.IsConnected)
			{
				TorSocks5Client = new TorSocks5Client(TorSocks5EndPoint!);
				await TorSocks5Client.ConnectAsync().ConfigureAwait(false);
				await TorSocks5Client.HandshakeAsync(IsolateStream, cancel).ConfigureAwait(false);
				await TorSocks5Client.ConnectToDestinationAsync(host, request.RequestUri.Port, cancel).ConfigureAwait(false);

				if (request.RequestUri.Scheme == "https")
				{
					await TorSocks5Client.UpgradeToSslAsync(host).ConfigureAwait(false);
				}
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
						request.Content = new ByteArrayContent(Array.Empty<byte>()); // dummy empty content
						request.Content.Headers.ContentLength = 0;
					}
					else
					{
						request.Content.Headers.ContentLength ??= (await request.Content.ReadAsStringAsync().ConfigureAwait(false)).Length;
					}
				}
			}

			string requestString = await request.ToHttpStringAsync().ConfigureAwait(false);

			var bytes = Encoding.UTF8.GetBytes(requestString);

			Stream transportStream = TorSocks5Client.GetTransportStream();

			await transportStream.WriteAsync(bytes, 0, bytes.Length, cancel).ConfigureAwait(false);
			await transportStream.FlushAsync(cancel).ConfigureAwait(false);

			return await HttpResponseMessageExtensions.CreateNewAsync(transportStream, request.Method).ConfigureAwait(false);
		}

		#region IDisposable Support

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
