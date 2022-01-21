using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Http.Extensions;

/// <summary>
/// Tests for <see cref="HttpRequestMessageExtensions"/>.
/// </summary>
public class HttpRequestMessageExtensionsTests
{
	[Fact]
	public async Task GetTestAsync()
	{
		using HttpRequestMessage request = new(HttpMethod.Get, "https://postman-echo.com");
		string plaintext = await HttpRequestMessageExtensions.ToHttpStringAsync(request, CancellationToken.None);

		Assert.Equal("GET / HTTP/1.1\r\nHost:postman-echo.com\r\n\r\n", plaintext);
	}

	[Fact]
	public async Task ConnectTestAsync()
	{
		using HttpRequestMessage request = new(new HttpMethod("CONNECT"), "https://postman-echo.com");
		string plaintext = await HttpRequestMessageExtensions.ToHttpStringAsync(request, CancellationToken.None);

		Assert.Equal("CONNECT / HTTP/1.1\r\n\r\n", plaintext);
	}

	[Fact]
	public async Task PostTestAsync()
	{
		// JSON string with HEX content.
		using var content = new StringContent(content: @"{""key"": ""value""}", Encoding.UTF8, "application/json");
		using HttpRequestMessage request = new(HttpMethod.Post, "https://postman-echo.com");
		request.Content = content;

		string actualPlaintext = await HttpRequestMessageExtensions.ToHttpStringAsync(request, CancellationToken.None);
		string expected = "POST / HTTP/1.1\r\nHost:postman-echo.com\r\nContent-Type:application/json; charset=utf-8\r\nContent-Length:16\r\n\r\n{\"key\": \"value\"}";

		Assert.Equal(expected, actualPlaintext);
	}
}
