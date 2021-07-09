using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class TorTests : IAsyncLifetime
	{
		public TorTests()
		{
			TorHttpPool = new(new TorTcpConnectionFactory(Common.TorSocks5Endpoint));
			TorManager = new(Common.TorSettings);
		}

		private TorHttpPool TorHttpPool { get; }
		private TorProcessManager TorManager { get; }

		public async Task InitializeAsync()
		{
			await TorManager.StartAsync();
		}

		public async Task DisposeAsync()
		{
			TorHttpPool.Dispose();
			await TorManager.DisposeAsync();
		}

		[Fact]
		public async Task CanDoRequestManyDifferentAsync()
		{
			TorHttpClient client = MakeTorHttpClient(new("http://api.qbit.ninja"));
			await QBitTestAsync(client, 10, alterRequests: true);
		}

		[Fact]
		public async Task CanRequestChunkEncodedAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient client = MakeTorHttpClient(new("http://anglesharp.azurewebsites.net/"));
			var response = await client.SendAsync(HttpMethod.Get, "Chunked", null, ctsTimeout.Token);
			var content = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
			Assert.Contains("Chunked transfer encoding test", content);
			Assert.Contains("This is a chunked response after 100 ms.", content);
			Assert.Contains("This is a chunked response after 1 second. The server should not close the stream before all chunks are sent to a client.", content);
		}

		[Fact]
		public async Task CanDoBasicPostHttpsRequestAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient client = MakeTorHttpClient(new("https://postman-echo.com"));
			using HttpContent content = new StringContent("This is expected to be sent back as part of response body.");

			HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "post", content, ctsTimeout.Token);
			var responseContentString = await message.Content.ReadAsStringAsync(ctsTimeout.Token);

			Assert.Contains("{\"args\":{},\"data\":\"This is expected to be sent back as part of response body.\"", responseContentString);
		}

		[Fact]
		public async Task TorIpIsNotTheRealOneAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			var requestUri = "https://api.ipify.org/";
			IPAddress? realIp;
			IPAddress? torIp;

			// 1. Get real IP
			using (HttpClient httpClient = new())
			{
				var content = await (await httpClient.GetAsync(requestUri)).Content.ReadAsStringAsync(ctsTimeout.Token);
				var gotIp = IPAddress.TryParse(content.Replace("\n", ""), out realIp);
				Assert.True(gotIp);
			}

			// 2. Get Tor IP
			{
				TorHttpClient torClient = MakeTorHttpClient(new(requestUri));
				var content = await (await torClient.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token)).Content.ReadAsStringAsync(ctsTimeout.Token);
				var gotIp = IPAddress.TryParse(content.Replace("\n", ""), out torIp);
				Assert.True(gotIp);
			}

			Assert.NotEqual(realIp, torIp);
		}

		[Fact]
		public async Task CanDoHttpsAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient client = MakeTorHttpClient(new("https://postman-echo.com"));
			using HttpResponseMessage httpResponseMessage = await client.SendAsync(HttpMethod.Get, "get?foo1=bar1&foo2=bar2", null, ctsTimeout.Token);
			var content = await httpResponseMessage.Content.ReadAsStringAsync(ctsTimeout.Token);

			Assert.Contains("{\"args\":{\"foo1\":\"bar1\",\"foo2\":\"bar2\"}", content);
		}

		[Fact]
		public async Task CanDoIpAddressAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient client = MakeTorHttpClient(new("http://172.217.6.142"));
			HttpResponseMessage httpResponseMessage = await client.SendAsync(HttpMethod.Get, relativeUri: "", content: null, ctsTimeout.Token);
			string content = await httpResponseMessage.Content.ReadAsStringAsync(ctsTimeout.Token);

			Assert.NotEmpty(content);
		}

		[Fact]
		public async Task CanRequestInRowAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient client = MakeTorHttpClient(new("http://api.qbit.ninja"));
			await (await client.SendAsync(HttpMethod.Get, "/transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json&headeronly=true", null, ctsTimeout.Token)).Content.ReadAsStringAsync(ctsTimeout.Token);
			await (await client.SendAsync(HttpMethod.Get, "/balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe?unspentonly=true", null, ctsTimeout.Token)).Content.ReadAsStringAsync(ctsTimeout.Token);
			await (await client.SendAsync(HttpMethod.Get, "balances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi?from=tip&until=336000", null, ctsTimeout.Token)).Content.ReadAsStringAsync(ctsTimeout.Token);
		}

		[Fact]
		public async Task CanRequestOnionV3Async()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient client = MakeTorHttpClient(new("http://www.dds6qkxpwdeubwucdiaord2xgbbeyds25rbsgr73tbfpqpt4a6vjwsyd.onion"));
			HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);
			var content = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			Assert.Contains("whonix", content, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task DoesntIsolateStreamsAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient c1 = MakeTorHttpClient(new("http://api.ipify.org"));
			TorHttpClient c2 = MakeTorHttpClient(new("http://api.ipify.org"));
			TorHttpClient c3 = MakeTorHttpClient(new("http://api.ipify.org"));
			Task<HttpResponseMessage> t1 = c1.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);
			Task<HttpResponseMessage> t2 = c2.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);
			Task<HttpResponseMessage> t3 = c3.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);

			var ips = new HashSet<IPAddress>
				{
					IPAddress.Parse(await (await t1).Content.ReadAsStringAsync(ctsTimeout.Token)),
					IPAddress.Parse(await (await t2).Content.ReadAsStringAsync(ctsTimeout.Token)),
					IPAddress.Parse(await (await t3).Content.ReadAsStringAsync(ctsTimeout.Token))
				};

			Assert.True(ips.Count < 3);
		}

		[Fact]
		public async Task IsolatesStreamsAsync()
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			TorHttpClient c1 = MakeTorHttpClient(new("http://api.ipify.org"), Mode.NewCircuitPerRequest);
			TorHttpClient c2 = MakeTorHttpClient(new("http://api.ipify.org"), Mode.NewCircuitPerRequest);
			TorHttpClient c3 = MakeTorHttpClient(new("http://api.ipify.org"), Mode.NewCircuitPerRequest);
			Task<HttpResponseMessage> t1 = c1.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);
			Task<HttpResponseMessage> t2 = c2.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);
			Task<HttpResponseMessage> t3 = c3.SendAsync(HttpMethod.Get, "", null, ctsTimeout.Token);

			var ips = new HashSet<IPAddress>
				{
					IPAddress.Parse(await (await t1).Content.ReadAsStringAsync(ctsTimeout.Token)),
					IPAddress.Parse(await (await t2).Content.ReadAsStringAsync(ctsTimeout.Token)),
					IPAddress.Parse(await (await t3).Content.ReadAsStringAsync(ctsTimeout.Token))
				};

			Assert.True(ips.Count >= 2); // very rarely it fails to isolate
		}

		[Fact]
		public async Task TorRunningAsync()
		{
			TorTcpConnectionFactory client1 = new(new IPEndPoint(IPAddress.Loopback, 37150));
			Assert.True(await client1.IsTorRunningAsync());

			TorTcpConnectionFactory client2 = new(new IPEndPoint(IPAddress.Loopback, 9054));
			Assert.False(await client2.IsTorRunningAsync());
		}

		private async Task<List<string>> QBitTestAsync(TorHttpClient client, int times, bool alterRequests = false)
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			var relativetUri = "/whatisit/what%20is%20my%20future";

			List<Task<HttpResponseMessage>> tasks = new();
			for (var i = 0; i < times; i++)
			{
				var task = client.SendAsync(HttpMethod.Get, relativetUri);
				if (alterRequests)
				{
					TorHttpClient ipClient = MakeTorHttpClient(new("https://api.ipify.org/"));
					var task2 = ipClient.SendAsync(HttpMethod.Get, "/", null, ctsTimeout.Token);
					tasks.Add(task2);
				}
				tasks.Add(task);
			}

			await Task.WhenAll(tasks);

			List<string> contents = new();
			foreach (var task in tasks)
			{
				contents.Add(await (await task).Content.ReadAsStringAsync(ctsTimeout.Token));
			}

			return contents;
		}

		private TorHttpClient MakeTorHttpClient(Uri uri, Mode mode = Mode.DefaultCircuit)
		{
			return new(uri, TorHttpPool, mode);
		}
	}
}
