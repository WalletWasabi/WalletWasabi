using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http.Interfaces
{
	public interface ITorHttpClient : IHttpClient, IDisposable
	{
		Uri DestinationUri { get; }
		Func<Uri> DestinationUriAction { get; }
		EndPoint TorSocks5EndPoint { get; }

		bool IsTorUsed { get; }

		Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default);

		/// <summary>
		/// Sends HTTP request (up to <paramref name="retry"/> times) to get HTTP response.
		/// </summary>
		/// <param name="retry">Maximum number of attempts to do to get HTTP response.</param>
		/// <exception cref="OperationCanceledException">When <paramref name="token"/> is used to cancel operation.</exception>
		public async Task<HttpResponseMessage?> SendAndRetryAsync(HttpMethod method, string relativeUri, int retry = 2, HttpContent? content = null, CancellationToken token = default)
		{
			HttpResponseMessage? response = null;

			while (retry-- > 0)
			{
				response?.Dispose();
				token.ThrowIfCancellationRequested();
				response = await SendAsync(method, relativeUri, content, cancel: token).ConfigureAwait(false);

				if (response.StatusCode == HttpStatusCode.OK)
				{
					break;
				}

				try
				{
					await Task.Delay(1000, token).ConfigureAwait(false);
				}
				catch (TaskCanceledException ex)
				{
					throw new OperationCanceledException(ex.Message, ex, token);
				}
			}
			return response;
		}
	}
}
