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

		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default);

		/// <remarks>
		/// Throws <see cref="OperationCanceledException"/> if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<HttpResponseMessage?> SendAndRetryAsync(HttpMethod method, HttpStatusCode expectedCode, string relativeUri, int retry = 2, HttpContent? content = null, CancellationToken cancel = default)
		{
			HttpResponseMessage? response = null;

			while (retry-- > 0)
			{
				response?.Dispose();
				cancel.ThrowIfCancellationRequested();
				response = await SendAsync(method, relativeUri, content, cancel: cancel).ConfigureAwait(false);

				if (response.StatusCode == expectedCode)
				{
					break;
				}

				try
				{
					await Task.Delay(1000, cancel).ConfigureAwait(false);
				}
				catch (TaskCanceledException ex)
				{
					throw new OperationCanceledException(ex.Message, ex, cancel);
				}
			}
			return response;
		}
	}
}
