using System.Net.Http;

namespace WalletWasabi.Tests.Helpers;

public static class HttpRequestMessageExtensions
{
	public static HttpRequestMessage Clone(this HttpRequestMessage request)
	{
		var clone = new HttpRequestMessage(request.Method, request.RequestUri)
		{
			Content = request.Content,
			Version = request.Version
		};
		foreach (var header in request.Headers)
		{
			clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		return clone;
	}
}
