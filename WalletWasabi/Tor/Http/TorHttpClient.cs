using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : IRelativeHttpClient
	{
		private static DateTimeOffset? TorDoesntWorkSinceBacking = null;

		public TorHttpClient(TorSocks5ClientPool pool, Uri baseUri, bool isolateStream = false) :
			this(pool, () => baseUri, isolateStream)
		{
			baseUri = Guard.NotNull(nameof(baseUri), baseUri);
		}

		public TorHttpClient(TorSocks5ClientPool pool, Func<Uri> baseUriAction, bool isolateStream = false)
		{
			DestinationUriAction = Guard.NotNull(nameof(baseUriAction), baseUriAction);
			DefaultIsolateStream = isolateStream;
			TorSocks5ClientPool = pool;
		}

		public static Exception? LatestTorException { get; private set; } = null;
		public Func<Uri> DestinationUriAction { get; }
		private EndPoint? TorSocks5EndPoint { get; }

		private TorSocks5ClientPool? TorSocks5ClientPool { get; }

		/// <inheritdoc/>
		public bool DefaultIsolateStream { get; }

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

		private Task<HttpResponseMessage> ClearnetRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			return new ClearnetHttpClient(DestinationUriAction).SendAsync(request, cancellationToken);
		}

		/// <remarks>
		/// Throws <see cref="OperationCanceledException"/> if <paramref name="token"/> is set.
		/// </remarks>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken token = default)
		{
			var requestUri = new Uri(DestinationUriAction(), relativeUri);
			using var request = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				request.Content = content;
			}

			return await SendAsync(request, DefaultIsolateStream, token).ConfigureAwait(false);
		}

		private static void SetTorNotWorkingState(Exception ex)
		{
			TorDoesntWorkSince ??= DateTimeOffset.UtcNow;
			LatestTorException = ex;
		}

		/// <exception cref="OperationCanceledException">If <paramref name="cancel"/> is set.</exception>
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			return SendAsync(request, DefaultIsolateStream, cancel);
		}

		/// <exception cref="OperationCanceledException">If <paramref name="token"/> is set.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default)
		{
			// Use clearnet HTTP client when Tor is disabled.
			if (TorSocks5EndPoint is null)
			{
				return await ClearnetRequestAsync(request, token).ConfigureAwait(false);
			}
			else
			{
				try
				{
					HttpResponseMessage httpResponseMessage = await TorSocks5ClientPool!.SendAsync(request, isolateStream, token).ConfigureAwait(false);
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
	}
}