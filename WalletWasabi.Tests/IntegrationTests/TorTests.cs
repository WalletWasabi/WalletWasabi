using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Http;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	// Tor must be running
	public class TorTests
	{
		public TorTests()
		{
			var torManager = new TorProcessManager(Global.Instance.TorSocks5Endpoint, Global.Instance.TorLogsFile);
			torManager.Start(ensureRunning: true, dataDir: Path.GetFullPath(AppContext.BaseDirectory));
			Task.Delay(3000).GetAwaiter().GetResult();
		}

		[Fact]
		public async Task CanDoRequestManyDifferentAsync()
		{
			using var client = new TorHttpClient(new Uri("http://api.qbit.ninja"), Global.Instance.TorSocks5Endpoint);
			await QBitTestAsync(client, 10, alterRequests: true);
		}

		[Fact]
		public async Task CanRequestChunkEncodedAsync()
		{
			using var client = new TorHttpClient(new Uri("http://anglesharp.azurewebsites.net/"), Global.Instance.TorSocks5Endpoint);
			var response = await client.SendAsync(HttpMethod.Get, "Chunked");
			var content = await response.Content.ReadAsStringAsync();
			Assert.Contains("Chunked transfer encoding test", content);
			Assert.Contains("This is a chunked response after 100 ms.", content);
			Assert.Contains("This is a chunked response after 1 second. The server should not close the stream before all chunks are sent to a client.", content);
		}

		[Fact]
		public async Task CanRequestClearnetAsync()
		{
			using var client = new TorHttpClient(new Uri("https://jigsaw.w3.org/"), null);
			var response = await client.SendAsync(HttpMethod.Get, "/HTTP/ChunkedScript");
			var content = await response.Content.ReadAsStringAsync();
			Assert.Equal(1000, Regex.Matches(content, "01234567890123456789012345678901234567890123456789012345678901234567890").Count);
		}

		[Fact]
		public async Task CanDoBasicPostHttpsRequestAsync()
		{
			using var client = new TorHttpClient(new Uri("https://postman-echo.com"), Global.Instance.TorSocks5Endpoint);
			HttpContent content = new StringContent("This is expected to be sent back as part of response body.");

			HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "post", content);
			var responseContentString = await message.Content.ReadAsStringAsync();

			Assert.Contains("{\"args\":{},\"data\":\"This is expected to be sent back as part of response body.\"", responseContentString);
		}

		[Fact]
		public async Task TorIpIsNotTheRealOneAsync()
		{
			var requestUri = "https://api.ipify.org/";
			IPAddress realIp;
			IPAddress torIp;

			// 1. Get real IP
			using (var httpClient = new HttpClient())
			{
				var content = await (await httpClient.GetAsync(requestUri)).Content.ReadAsStringAsync();
				var gotIp = IPAddress.TryParse(content.Replace("\n", ""), out realIp);
				Assert.True(gotIp);
			}

			// 2. Get Tor IP
			using (var client = new TorHttpClient(new Uri(requestUri), Global.Instance.TorSocks5Endpoint))
			{
				var content = await (await client.SendAsync(HttpMethod.Get, "")).Content.ReadAsStringAsync();
				var gotIp = IPAddress.TryParse(content.Replace("\n", ""), out torIp);
				Assert.True(gotIp);
			}

			Assert.NotEqual(realIp, torIp);
		}

		[Fact]
		public async Task CanDoHttpsAsync()
		{
			using var client = new TorHttpClient(new Uri("https://postman-echo.com"), Global.Instance.TorSocks5Endpoint);
			var content = await (await client.SendAsync(HttpMethod.Get, "get?foo1=bar1&foo2=bar2")).Content.ReadAsStringAsync();

			Assert.Contains("{\"args\":{\"foo1\":\"bar1\",\"foo2\":\"bar2\"}", content);
		}

		[Fact]
		public async Task CanDoIpAddressAsync()
		{
			using var client = new TorHttpClient(new Uri("http://172.217.6.142"), Global.Instance.TorSocks5Endpoint);
			var content = await (await client.SendAsync(HttpMethod.Get, "")).Content.ReadAsStringAsync();

			Assert.NotEmpty(content);
		}

		[Fact]
		public async Task CanRequestInRowAsync()
		{
			using var client = new TorHttpClient(new Uri("http://api.qbit.ninja"), Global.Instance.TorSocks5Endpoint);
			await (await client.SendAsync(HttpMethod.Get, "/transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json&headeronly=true")).Content.ReadAsStringAsync();
			await (await client.SendAsync(HttpMethod.Get, "/balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe?unspentonly=true")).Content.ReadAsStringAsync();
			await (await client.SendAsync(HttpMethod.Get, "balances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi?from=tip&until=336000")).Content.ReadAsStringAsync();
		}

		[Fact]
		public async Task CanRequestOnionV2Async()
		{
			using var client = new TorHttpClient(new Uri("http://expyuzz4wqqyqhjn.onion/"), Global.Instance.TorSocks5Endpoint);
			HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "");
			var content = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			Assert.Contains("tor", content, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task CanRequestOnionV3Async()
		{
			using var client = new TorHttpClient(new Uri("http://www.dds6qkxpwdeubwucdiaord2xgbbeyds25rbsgr73tbfpqpt4a6vjwsyd.onion"), Global.Instance.TorSocks5Endpoint);
			HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "");
			var content = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			Assert.Contains("whonix", content, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task DoesntIsolateStreamsAsync()
		{
			using var c1 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint);
			using var c2 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint);
			using var c3 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint);
			var t1 = c1.SendAsync(HttpMethod.Get, "");
			var t2 = c2.SendAsync(HttpMethod.Get, "");
			var t3 = c3.SendAsync(HttpMethod.Get, "");

			var ips = new HashSet<IPAddress>
				{
					IPAddress.Parse(await (await t1).Content.ReadAsStringAsync()),
					IPAddress.Parse(await (await t2).Content.ReadAsStringAsync()),
					IPAddress.Parse(await (await t3).Content.ReadAsStringAsync())
				};

			Assert.True(ips.Count < 3);
		}

		[Fact]
		public async Task IsolatesStreamsAsync()
		{
			using var c1 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint, isolateStream: true);
			using var c2 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint, isolateStream: true);
			using var c3 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint, isolateStream: true);
			var t1 = c1.SendAsync(HttpMethod.Get, "");
			var t2 = c2.SendAsync(HttpMethod.Get, "");
			var t3 = c3.SendAsync(HttpMethod.Get, "");

			var ips = new HashSet<IPAddress>
				{
					IPAddress.Parse(await (await t1).Content.ReadAsStringAsync()),
					IPAddress.Parse(await (await t2).Content.ReadAsStringAsync()),
					IPAddress.Parse(await (await t3).Content.ReadAsStringAsync())
				};

			Assert.True(ips.Count >= 2); // very rarely it fails to isolate
		}

		[Fact]
		public async Task TorRunningAsync()
		{
			Assert.True(await TorProcessManager.IsTorRunningAsync(null));
			Assert.True(await TorProcessManager.IsTorRunningAsync(new IPEndPoint(IPAddress.Loopback, 9050)));
			Assert.False(await TorProcessManager.IsTorRunningAsync(new IPEndPoint(IPAddress.Loopback, 9054)));
		}

		private static async Task<List<string>> QBitTestAsync(TorHttpClient client, int times, bool alterRequests = false)
		{
			var relativetUri = "/whatisit/what%20is%20my%20future";

			var tasks = new List<Task<HttpResponseMessage>>();
			for (var i = 0; i < times; i++)
			{
				var task = client.SendAsync(HttpMethod.Get, relativetUri);
				if (alterRequests)
				{
					using var ipClient = new TorHttpClient(new Uri("https://api.ipify.org/"), Global.Instance.TorSocks5Endpoint);
					var task2 = ipClient.SendAsync(HttpMethod.Get, "/");
					tasks.Add(task2);
				}
				tasks.Add(task);
			}

			await Task.WhenAll(tasks);

			var contents = new List<string>();
			foreach (var task in tasks)
			{
				contents.Add(await (await task).Content.ReadAsStringAsync());
			}

			return contents;
		}
	}
}
