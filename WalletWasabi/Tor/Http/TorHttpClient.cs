using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : IRelativeHttpClient
	{
		public TorHttpClient(TorSocks5ClientPool pool, Func<Uri> baseUriAction, bool isolateStream = false)
		{
			DestinationUriAction = baseUriAction;
			DefaultIsolateStream = isolateStream;
			TorSocks5ClientPool = pool;
		}

		/// <inheritdoc/>
		public Func<Uri> DestinationUriAction { get; }

		/// <inheritdoc/>
		public bool DefaultIsolateStream { get; }

		private TorSocks5ClientPool TorSocks5ClientPool { get; }

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

		/// <exception cref="OperationCanceledException">If <paramref name="cancel"/> is set.</exception>
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			return SendAsync(request, DefaultIsolateStream, cancel);
		}

		/// <exception cref="OperationCanceledException">If <paramref name="token"/> is set.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default)
		{
			return await TorSocks5ClientPool!.SendAsync(request, isolateStream, token).ConfigureAwait(false);
		}
	}
}