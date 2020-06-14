using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.TorSocks5;

public static class TorHttpClientExtensions
{
	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public static async Task<HttpResponseMessage> SendAndRetryAsync(this ITorHttpClient client, HttpMethod method, HttpStatusCode expectedCode, string relativeUri, int retry = 2, HttpContent content = null, CancellationToken cancel = default)
	{
		HttpResponseMessage response = null;
		while (retry-- > 0)
		{
			response?.Dispose();
			cancel.ThrowIfCancellationRequested();
			response = await client.SendAsync(method, relativeUri, content, cancel: cancel);
			if (response.StatusCode == expectedCode)
			{
				break;
			}
			try
			{
				await Task.Delay(1000, cancel);
			}
			catch (TaskCanceledException ex)
			{
				throw new OperationCanceledException(ex.Message, ex, cancel);
			}
		}
		return response;
	}
}
