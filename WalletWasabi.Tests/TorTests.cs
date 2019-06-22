using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using Xunit;

namespace WalletWasabi.Tests
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
			using (var client = new TorHttpClient(new Uri("http://api.qbit.ninja"), Global.Instance.TorSocks5Endpoint))
			{
				await QBitTestAsync(client, 10, alterRequests: true);
			}
		}

		[Fact]
		public async Task CanRequestChunkEncodedAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://jigsaw.w3.org/"), Global.Instance.TorSocks5Endpoint))
			{
				var response = await client.SendAsync(HttpMethod.Get, "/HTTP/ChunkedScript");
				var content = await response.Content.ReadAsStringAsync();
				Assert.Equal(1000, Regex.Matches(content, "01234567890123456789012345678901234567890123456789012345678901234567890").Count);
			}
		}

		[Fact]
		public async Task CanRequestClearnetAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://jigsaw.w3.org/"), null))
			{
				var response = await client.SendAsync(HttpMethod.Get, "/HTTP/ChunkedScript");
				var content = await response.Content.ReadAsStringAsync();
				Assert.Equal(1000, Regex.Matches(content, "01234567890123456789012345678901234567890123456789012345678901234567890").Count);
			}
		}

		[Fact]
		public async Task CanDoBasicPostHttpsRequestAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://api.smartbit.com.au"), Global.Instance.TorSocks5Endpoint))
			{
				HttpContent content = new StringContent("{\"hex\": \"01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff2d03a58605204d696e656420627920416e74506f6f6c20757361311f10b53620558903d80272a70c0000724c0600ffffffff010f9e5096000000001976a9142ef12bd2ac1416406d0e132e5bc8d0b02df3861b88ac00000000\"}");

				HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "/v1/blockchain/decodetx", content);
				var responseContentString = await message.Content.ReadAsStringAsync();

				Assert.Equal("{\"success\":true,\"transaction\":{\"Version\":\"1\",\"LockTime\":\"0\",\"Vin\":[{\"TxId\":null,\"Vout\":null,\"ScriptSig\":null,\"CoinBase\":\"03a58605204d696e656420627920416e74506f6f6c20757361311f10b53620558903d80272a70c0000724c0600\",\"TxInWitness\":null,\"Sequence\":\"4294967295\"}],\"Vout\":[{\"Value\":25.21865743,\"N\":0,\"ScriptPubKey\":{\"Asm\":\"OP_DUP OP_HASH160 2ef12bd2ac1416406d0e132e5bc8d0b02df3861b OP_EQUALVERIFY OP_CHECKSIG\",\"Hex\":\"76a9142ef12bd2ac1416406d0e132e5bc8d0b02df3861b88ac\",\"ReqSigs\":1,\"Type\":\"pubkeyhash\",\"Addresses\":[\"15HCzh8AoKRnTWMtmgAsT9TKUPrQ6oh9HQ\"]}}],\"TxId\":\"a02b9bd4264ab5d7c43ee18695141452b23b230b2a8431b28bbe446bf2b2f595\"}}", responseContentString);
			}
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
			using (var client = new TorHttpClient(new Uri("https://slack.com"), Global.Instance.TorSocks5Endpoint))
			{
				var content =
					await (await client.SendAsync(HttpMethod.Get, "api/api.test")).Content.ReadAsStringAsync();

				Assert.Equal("{\"ok\":true}", content);
			}
		}

		[Fact]
		public async Task CanDoIpAddressAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://172.217.6.142"), Global.Instance.TorSocks5Endpoint))
			{
				var content = await (await client.SendAsync(HttpMethod.Get, "")).Content.ReadAsStringAsync();

				Assert.NotEmpty(content);
			}
		}

		[Fact]
		public async Task CanRequestInRowAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://api.qbit.ninja"), Global.Instance.TorSocks5Endpoint))
			{
				await (await client.SendAsync(HttpMethod.Get, "/transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json&headeronly=true")).Content.ReadAsStringAsync();
				await (await client.SendAsync(HttpMethod.Get, "/balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe?unspentonly=true")).Content.ReadAsStringAsync();
				await (await client.SendAsync(HttpMethod.Get, "balances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi?from=tip&until=336000")).Content.ReadAsStringAsync();
			}
		}

		[Fact]
		public async Task CanRequestOnionV2Async()
		{
			using (var client = new TorHttpClient(new Uri("http://expyuzz4wqqyqhjn.onion/"), Global.Instance.TorSocks5Endpoint))
			{
				HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "");
				var content = await response.Content.ReadAsStringAsync();

				Assert.Equal(HttpStatusCode.OK, response.StatusCode);

				Assert.Contains("tor", content, StringComparison.OrdinalIgnoreCase);
			}
		}

		[Fact]
		public async Task CanRequestOnionV3Async()
		{
			using (var client = new TorHttpClient(new Uri("http://dds6qkxpwdeubwucdiaord2xgbbeyds25rbsgr73tbfpqpt4a6vjwsyd.onion"), Global.Instance.TorSocks5Endpoint))
			{
				HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "");
				var content = await response.Content.ReadAsStringAsync();

				Assert.Equal(HttpStatusCode.OK, response.StatusCode);

				Assert.Contains("whonix", content, StringComparison.OrdinalIgnoreCase);
			}
		}

		[Fact]
		public async Task DoesntIsolateStreamsAsync()
		{
			using (var c1 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint))
			using (var c2 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint))
			using (var c3 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint))
			{
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
		}

		[Fact]
		public async Task IsolatesStreamsAsync()
		{
			using (var c1 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint, isolateStream: true))
			using (var c2 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint, isolateStream: true))
			using (var c3 = new TorHttpClient(new Uri("http://api.ipify.org"), Global.Instance.TorSocks5Endpoint, isolateStream: true))
			{
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
					using (var ipClient = new TorHttpClient(new Uri("https://api.ipify.org/"), Global.Instance.TorSocks5Endpoint))
					{
						var task2 = ipClient.SendAsync(HttpMethod.Get, "/");
						tasks.Add(task2);
					}
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
