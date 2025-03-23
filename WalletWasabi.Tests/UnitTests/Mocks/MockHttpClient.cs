using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests;

public class MockHttpClient : HttpClient
{
	public Func<HttpRequestMessage, Task<HttpResponseMessage>>? OnSendAsync { get; set; }

	public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
		OnSendAsync?.Invoke(request) ?? throw new NotImplementedException();
}
