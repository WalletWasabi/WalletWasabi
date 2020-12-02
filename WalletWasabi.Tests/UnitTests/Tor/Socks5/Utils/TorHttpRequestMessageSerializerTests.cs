using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5.Utils;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5.Utils
{
	/// <summary>
	/// Tests for <see cref="TorHttpRequestMessageSerializer"/>.
	/// </summary>
	public class TorHttpRequestMessageSerializerTests
	{
		[Fact]
		public async Task GetTestAsync()
		{
			using HttpRequestMessage request = new(HttpMethod.Get, "https://postman-echo.com");
			string plaintext = await TorHttpRequestMessageSerializer.ToStringAsync(request, CancellationToken.None);

			Assert.Equal("GET / HTTP/1.1\r\nHost:postman-echo.com\r\n\r\n", plaintext);
		}

		[Fact]
		public async Task PostTestAsync()
		{
			// JSON string with HEX content.
			using var content = new StringContent(content: @"{""key"": ""value""}", Encoding.UTF8, "application/json");
			using HttpRequestMessage request = new(HttpMethod.Post, "https://postman-echo.com");
			request.Content = content;

			string actualPlaintext = await TorHttpRequestMessageSerializer.ToStringAsync(request, CancellationToken.None);
			string expected = "POST / HTTP/1.1\r\nHost:postman-echo.com\r\nContent-Type:application/json; charset=utf-8\r\nContent-Length:16\r\n\r\n{\"key\": \"value\"}";

			Assert.Equal(expected, actualPlaintext);
		}
	}
}