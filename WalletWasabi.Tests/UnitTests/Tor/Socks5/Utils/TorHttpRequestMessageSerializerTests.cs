using System.Net.Http;
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
		public async Task BasicTestAsync()
		{
			using HttpRequestMessage request = new(HttpMethod.Get, "https://postman-echo.com");
			string plaintext = await TorHttpRequestMessageSerializer.ToStringAsync(request, CancellationToken.None);

			Assert.Equal("GET / HTTP/1.1\r\nHost:postman-echo.com\r\n\r\n", plaintext);
		}
	}
}