using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

public class StuttererHttpClient(HttpClient httpClient) : HttpClient
{
	public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
	{
		using HttpRequestMessage requestClone1 = request.Clone();
		using HttpRequestMessage requestClone2 = request.Clone();

		HttpResponseMessage result1 = await httpClient.SendAsync(requestClone1, token).ConfigureAwait(false);
		HttpResponseMessage result2 = await httpClient.SendAsync(requestClone2, token).ConfigureAwait(false);

		string content1 = await result1.Content.ReadAsStringAsync(token).ConfigureAwait(false);
		string content2 = await result2.Content.ReadAsStringAsync(token).ConfigureAwait(false);

		Assert.Equal(content1, content2);
		return result2;
	}
}
