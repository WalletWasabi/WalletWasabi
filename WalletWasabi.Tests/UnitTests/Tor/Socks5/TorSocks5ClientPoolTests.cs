using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5
{
	/// <summary>
	/// Tests for <see cref="TorSocks5ClientPool"/>
	/// </summary>
	public class TorSocks5ClientPoolTests
	{
		/// <summary>
		/// TODO.
		/// <summary>
		[Fact]
		public async Task TestSendingAsync()
		{
			using CancellationTokenSource timeoutCts = new(millisecondsDelay: 10_000);

			ClearnetHttpClient httpClient = new();
			TorPoolItemManager poolItemManager = new(maxPoolItemsPerHost: 2);

			using MemoryStream transportStream = new();
			StreamReader transportStreamReader = new(transportStream);
			TestPoolItemFactory testPoolItemFactory = new(transportStream);

			TorSocks5ClientPool pool = new(httpClient, poolItemManager, testPoolItemFactory.CreateNewAsync);

			// HTTPS -> cannot be re-used.
			using HttpRequestMessage requestMessage = new(HttpMethod.Get, "https://postman-echo.com");
			using HttpResponseMessage httpResponseMessage = await pool.SendAsync(requestMessage, isolateStream: true, timeoutCts.Token);

			string actualContent = transportStreamReader.ReadToEnd();
			Assert.Equal("", actualContent);
		}
	}

	public class TestPoolItemFactory
	{
		public TestPoolItemFactory(Stream transportStream)
		{
			TransportStream = transportStream;
		}

		public Stream TransportStream { get; }

		[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Cancellation token is required by the delegate.")]
		public Task<IPoolItem> CreateNewAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default)
		{
			bool useSsl = request.RequestUri!.Scheme == Uri.UriSchemeHttps;
			bool allowRecycling = !useSsl && !isolateStream;

			return Task.FromResult<IPoolItem>(new TestPoolItem(PoolItemState.InUse, allowRecycling, TransportStream));
		}
	}
}