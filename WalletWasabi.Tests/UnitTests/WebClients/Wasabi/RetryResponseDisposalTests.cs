using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WebClients.Wasabi;

public class RetryResponseDisposalTests
{
	private sealed class TrackedContent() : StringContent("retry response")
	{
		public bool Disposed { get; private set; }
		protected override void Dispose(bool disposing)
		{
			Disposed |= disposing;
			base.Dispose(disposing);
		}
	}

	[Theory]
	[InlineData(HttpStatusCode.RequestTimeout)]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	[InlineData(HttpStatusCode.TooManyRequests)]
	public async Task RetriedResponseIsDisposedBeforeNextAttemptAsync(HttpStatusCode status)
	{
		using var content = new TrackedContent();
		var count = 0;
		using var handler = new RetryHttpClientHandler("disposal-test", _ => { },
			HttpClientHandlerConfiguration.Default with
			{
				TimeBeforeRetryingAfterServerError = TimeSpan.Zero,
				TimeBeforeRetryingAfterTooManyRequests = TimeSpan.Zero
			}, (_, _, _) =>
			{
				if (Interlocked.Increment(ref count) == 1)
				{ return Task.FromResult(new HttpResponseMessage(status) { Content = content }); }
				Assert.True(content.Disposed);
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			});
		using var client = new HttpClient(handler);
		using var response = await client.GetAsync("https://fixture.invalid");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(2, count);
	}
}
