using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WebClients.Wasabi;

public class RetryHttpClientHandlerTests
{
	// Trivial test to make sure that the mock handler works as expected.
	[Fact]
	public async Task SendAsync_OkTestAsync()
	{
		var callbackCalled = false;

		{
			using var handler = new MockRetryHttpClientHandler(handlerName => callbackCalled = true, HttpClientHandlerConfiguration.Default)
			{
				GetResponseAsync = async handler =>
				{
					var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
					responseMessage.Content = new StringContent("My Response", Encoding.UTF8, MediaTypeNames.Text.Plain);
					return responseMessage;
				}
			};

			using var httpClient = new HttpClient(handler, disposeHandler: false);
			using var request = new HttpRequestMessage(HttpMethod.Get, "http://test.dev");

			using var response = await httpClient.SendAsync(request);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			using var stringContent = Assert.IsType<StringContent>(response.Content);
			var payload = await stringContent.ReadAsStringAsync();
			Assert.Equal("My Response", payload);
		}

		Assert.True(callbackCalled);
	}

	// Tests that the handler stops retrying if .
	[Fact]
	public async Task SendAsync_RepeatingStopsAsync()
	{
		var callbackCalled = false;
		var requestsCount = 0;

		{
			using var handler = new MockRetryHttpClientHandler(handlerName => callbackCalled = true, HttpClientHandlerConfiguration.Default)
			{
				GetResponseAsync = async handler =>
				{
					requestsCount++;

					if (requestsCount == 1)
					{
						// Simulate a disposed handler on the first request.
						handler.Dispose();

						// Force the HTTP handler to repeat the request.
						throw new HttpRequestException(HttpRequestError.ConnectionError, "Make sure the request is repeated.");
					}
					else
					{
						throw new UnreachableException();
					}
				}
			};

			using var httpClient = new HttpClient(handler, disposeHandler: false);
			using var request = new HttpRequestMessage(HttpMethod.Get, "http://test.dev");

			// Expect an ObjectDisposedException since the handler was disposed during the first request.
			var e = await Assert.ThrowsAsync<TimeoutException>(async () => await httpClient.SendAsync(request).ConfigureAwait(false));
			Assert.Equal($"HTTP handler 'MockRetryHttpClientHandler' was disposed during request.", e.Message);
		}

		Assert.Equal(1, requestsCount);
		Assert.True(callbackCalled);
	}

	// Tests that an ObjectDisposedException is thrown when the handler is disposed before sending an HTTP request.
	[Fact]
	public async Task SendAsync_DisposeExceptionIsThrownAsync()
	{
		var callbackCalled = false;

		using var handler = new MockRetryHttpClientHandler(handlerName => callbackCalled = true, HttpClientHandlerConfiguration.Default)
		{
			GetResponseAsync = async handler =>
			{
				var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
				responseMessage.Content = new StringContent("My Response", Encoding.UTF8, MediaTypeNames.Text.Plain);
				return responseMessage;
			}
		};

		using (var httpClient = new HttpClient(handler, disposeHandler: false))
		{
			// Dispose the handler to trigger the disposed exception.
			handler.Dispose();

			// Send a request after disposing the handler.
			using var request = new HttpRequestMessage(HttpMethod.Get, "http://test.dev");

			// Expect an ObjectDisposedException since the handler was disposed before sending the request.
			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await httpClient.SendAsync(request).ConfigureAwait(false));
		}

		Assert.True(callbackCalled);
	}
}

file class MockRetryHttpClientHandler : RetryHttpClientHandler
{
	public MockRetryHttpClientHandler(Action<string> disposedCallback, HttpClientHandlerConfiguration config)
		: base(nameof(MockRetryHttpClientHandler), disposedCallback, config)
	{
	}

	public required Func<HttpClientHandler, Task<HttpResponseMessage>> GetResponseAsync { get; init; }

	protected override async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage responseMessage = await GetResponseAsync(this).ConfigureAwait(false);
		return responseMessage;
	}
}
