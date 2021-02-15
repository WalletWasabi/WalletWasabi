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
	public class TorHttpClient : IHttpClient, IDisposable
	{
		private static DateTimeOffset? TorDoesntWorkSinceBacking = null;

		private volatile bool _disposedValue = false; // To detect redundant calls

		public TorHttpClient(Uri baseUri, EndPoint torSocks5EndPoint, bool isolateStream = false) :
			this(() => baseUri, torSocks5EndPoint, isolateStream)
		{
			baseUri = Guard.NotNull(nameof(baseUri), baseUri);
		}

		public TorHttpClient(Func<Uri> baseUriGetter, EndPoint torSocks5EndPoint, bool isolateStream = false)
		{
			BaseUriGetter = Guard.NotNull(nameof(baseUriGetter), baseUriGetter);
			Guard.NotNull(nameof(torSocks5EndPoint), torSocks5EndPoint);

			TorSocks5EndPoint = torSocks5EndPoint;
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

		public Func<Uri> BaseUriGetter { get; }
		private EndPoint TorSocks5EndPoint { get; }

		/// <summary>
		/// Whether each HTTP(s) request should use a separate Tor circuit or not to increase privacy.
		/// </summary>
		public bool IsolateStream { get; }

		private TorSocks5Client? TorSocks5Client { get; set; }

		private static AsyncLock AsyncLock { get; } = new AsyncLock(); // We make everything synchronous, so slow, but at least stable.	

		/// <exception cref="HttpRequestException">When HTTP request fails to be processed. Inner exception may be an instance of <see cref="TorException"/>.</exception>
		/// <exception cref="OperationCanceledException">When <paramref name="cancel"/> is canceled by the user.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			Guard.NotNull(nameof(method), method);
			relativeUri = Guard.NotNull(nameof(relativeUri), relativeUri);
			var requestUri = new Uri(BaseUriGetter(), relativeUri);
			using var request = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				request.Content = content;
			}

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
			catch (TorException ex)
			{
				SetTorNotWorkingState(ex);

				// Wrap exception to unify ClearnetHttpClient and TorHttpClient exception throwing behavior.
				throw new HttpRequestException("Failed to handle the HTTP request via Tor.", inner: ex);
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

		/// <exception cref="OperationCanceledException">If <paramref name="token"/> is set.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			string host = Guard.NotNullOrEmptyOrWhitespace($"{nameof(request)}.{nameof(request.RequestUri)}.{nameof(request.RequestUri.DnsSafeHost)}", request.RequestUri!.DnsSafeHost, trim: true);

			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries
			// other than those acting as tunnels) MUST send their own HTTP - version
			// in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;
			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

			if (TorSocks5Client is null)
			{
				try
				{
					TorSocks5Client = new TorSocks5Client(TorSocks5EndPoint!);
					await TorSocks5Client.ConnectAsync(token).ConfigureAwait(false);
					await TorSocks5Client.HandshakeAsync(IsolateStream, token).ConfigureAwait(false);
					await TorSocks5Client.ConnectToDestinationAsync(host, request.RequestUri.Port, token).ConfigureAwait(false);

					if (request.RequestUri.Scheme == "https")
					{
						await TorSocks5Client.UpgradeToSslAsync(host).ConfigureAwait(false);
					}
				}
				catch
				{
					TorSocks5Client?.Dispose();
					TorSocks5Client = null;
					throw;
				}
			}

			// At this point Tor SOCKS5 client is always non-null.
			token.ThrowIfCancellationRequested();

			string requestString = await request.ToHttpStringAsync(token).ConfigureAwait(false);

			var bytes = Encoding.UTF8.GetBytes(requestString);

			Stream transportStream = TorSocks5Client.GetTransportStream();

			try
			{
				await transportStream.WriteAsync(bytes.AsMemory(0, bytes.Length), token).ConfigureAwait(false);
				await transportStream.FlushAsync(token).ConfigureAwait(false);

				return await HttpResponseMessageExtensions.CreateNewAsync(transportStream, request.Method).ConfigureAwait(false);
			}
			catch
			{
				TorSocks5Client?.Dispose();
				TorSocks5Client = null;
				throw;
			}
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
