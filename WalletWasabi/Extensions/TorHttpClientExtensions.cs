using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public static class HttpClientExtensions
{
	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public static async Task<HttpResponseMessage> SendAndRetryAsync(this HttpClient client, HttpMethod method, HttpStatusCode expectedCode, string relativeUri, int retry = 2, HttpContent content = null, CancellationToken cancel = default)
	{
		HttpResponseMessage response = null;
		while (retry-- > 0)
		{
			response?.Dispose();
			cancel.ThrowIfCancellationRequested();
			var request = new HttpRequestMessage(method, relativeUri)
			{
				Content = content
			};
			response = await client.SendAsync(request, cancel);
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
