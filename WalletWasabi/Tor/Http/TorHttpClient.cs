using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : IHttpClient
	{
		/// <summary>Use this constructor when you want to issue absolute HTTP requests.</summary>
		public TorHttpClient(TorHttpPool torHttpPool, bool isolateStream = false) :
			this(baseUriGetter: null, torHttpPool, isolateStream)
		{
		}

		/// <summary>Use this constructor when you want to issue relative or absolute HTTP requests.</summary>
		public TorHttpClient(Uri baseUri, TorHttpPool torHttpPool, bool isolateStream = false) :
			this(() => baseUri, torHttpPool, isolateStream)
		{
		}

		/// <summary>Use this constructor when you want to issue relative or absolute HTTP requests.</summary>
		public TorHttpClient(Func<Uri>? baseUriGetter, TorHttpPool torHttpPool, bool isolateStream = false)
		{
			BaseUriGetter = baseUriGetter;
			TorHttpPool = torHttpPool;
			IsolateStream = isolateStream;
		}

		public Func<Uri>? BaseUriGetter { get; }

		/// <summary>Whether each HTTP(s) request should use a separate Tor circuit or not to increase privacy.</summary>
		public bool IsolateStream { get; }

		private TorHttpPool TorHttpPool { get; }

		/// <exception cref="HttpRequestException">When HTTP request fails to be processed. Inner exception may be an instance of <see cref="TorException"/>.</exception>
		/// <exception cref="OperationCanceledException">When <paramref name="token"/> is canceled by the user.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken token = default)
		{
			if (BaseUriGetter is null)
			{
				throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
			}

			Uri requestUri = new(BaseUriGetter(), relativeUri);
			using HttpRequestMessage request = new(method, requestUri);

			if (content is { })
			{
				request.Content = content;
			}

			return await SendAsync(request, token).ConfigureAwait(false);
		}

		/// <exception cref="OperationCanceledException">If <paramref name="token"/> is set.</exception>
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return TorHttpPool.SendAsync(request, IsolateStream, token);
		}

		public Task<bool> IsTorRunningAsync()
		{
			return TorHttpPool.IsTorRunningAsync();
		}
	}
}
