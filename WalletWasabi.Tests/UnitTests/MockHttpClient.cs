using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.UnitTests;

public class MockHttpClient : IHttpClient
{
	public MockHttpClient()
		: this("https://fake.domain.com")
	{}

	public MockHttpClient(string uri)
	{
		BaseUriGetter = () => new Uri(uri);
	}
	public Func<Uri>? BaseUriGetter { get; }
	public Func<HttpRequestMessage, Task<HttpResponseMessage>>? OnSendAsync { get; set; }

	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
		OnSendAsync?.Invoke(request) ?? throw new NotImplementedException();
}
