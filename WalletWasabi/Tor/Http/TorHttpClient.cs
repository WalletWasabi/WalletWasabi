using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : IHttpClient
	{
		public TorHttpClient(TorSocks5ClientPool pool, Func<Uri> baseUriAction, bool isolateStream = false)
		{
			BaseUriGetter = baseUriAction;
			DefaultIsolateStream = isolateStream;
			TorSocks5ClientPool = pool;
		}

		/// <inheritdoc/>
		public Func<Uri> BaseUriGetter { get; }

		/// <inheritdoc/>
		public bool DefaultIsolateStream { get; }

		private TorSocks5ClientPool TorSocks5ClientPool { get; }

		/// <exception cref="HttpRequestException">When HTTP request fails to be processed. Inner exception may be an instance of <see cref="TorException"/>.</exception>
		/// <exception cref="OperationCanceledException">When <paramref name="cancel"/> is canceled by the user.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken token = default)
		{
			var requestUri = new Uri(BaseUriGetter(), relativeUri);
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
			return await TorSocks5ClientPool.SendAsync(request, isolateStream, token).ConfigureAwait(false);
		}
	}
}