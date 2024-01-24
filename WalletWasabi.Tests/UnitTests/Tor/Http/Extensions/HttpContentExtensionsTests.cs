using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Http.Extensions;

/// <summary>
/// Tests for <see cref="HttpContentExtensions"/>.
/// </summary>
public class HttpContentExtensionsTests
{
	[Fact]
	public async Task ReadAsJsonAsync()
	{
		// null is valid JSON value but we are interested only in deserialized objects that are non-null.
		{
			using StringContent content = new(content: "null");
			await Assert.ThrowsAsync<InvalidOperationException>(content.ReadAsJsonAsync<TestClass>);
		}

		// Invalid JSON. We should throw an exception that inherits from `JsonException`.
		{
			using StringContent content = new(content: """{ "property" : error-here """);
			await Assert.ThrowsAnyAsync<JsonException>(content.ReadAsJsonAsync<TestClass>);
		}
	}

	private class TestClass
	{
		[JsonProperty(PropertyName = "blockHeight")]
		public int BlockHeight { get; set; }
	}
}
