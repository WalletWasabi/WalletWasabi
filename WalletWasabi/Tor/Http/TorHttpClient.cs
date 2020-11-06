using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Http.Interfaces;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : ITorHttpClient, IDisposable
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
			TorSocks5EndPoint = DestinationUri.IsLoopback ? null : torSocks5EndPoint;

			if (TorSocks5EndPoint is { })
			{
				TorSocks5ClientPool = new TorSocks5ClientPool(TorSocks5EndPoint, isolateStream);
			}
			else
			{
				TorSocks5EndPoint = null!;
			}
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

		public Uri DestinationUri => DestinationUriAction();
		public Func<Uri> DestinationUriAction { get; }

		// TODO: Make it private.
		public EndPoint? TorSocks5EndPoint { get; private set; }
		public bool IsTorUsed => TorSocks5EndPoint is { };

		private TorSocks5ClientPool TorSocks5ClientPool { get; }

		private static async Task<HttpResponseMessage> ClearnetRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			return await ClearnetHttpClient.Instance.SendAsync(request, cancellationToken).ConfigureAwait(false);
		}

		/// <remarks>
		/// Throws <see cref="OperationCanceledException"/> if <paramref name="token"/> is set.
		/// </remarks>
		public Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken token = default)
		{
			Guard.NotNull(nameof(method), method);
			relativeUri = Guard.NotNull(nameof(relativeUri), relativeUri);
			var requestUri = new Uri(DestinationUri, relativeUri);
			using var request = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				request.Content = content;
			}

			return SendAsync(request, token);
		}

		private static void SetTorNotWorkingState(Exception ex)
		{
			TorDoesntWorkSince ??= DateTimeOffset.UtcNow;
			LatestTorException = ex;
		}

		/// <exception cref="OperationCanceledException">If <paramref name="token"/> is set.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			Guard.NotNull(nameof(request), request);

			// Use clearnet HTTP client when Tor is disabled.
			if (TorSocks5EndPoint is null)
			{
				return await ClearnetRequestAsync(request, token).ConfigureAwait(false);
			}
			else
			{
				try
				{
					HttpResponseMessage httpResponseMessage = await TorSocks5ClientPool.SendAsync(request, token).ConfigureAwait(false);
					TorDoesntWorkSince = null;

					return httpResponseMessage;
				}
				catch (OperationCanceledException ex)
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
					TorSocks5ClientPool.Dispose();
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
