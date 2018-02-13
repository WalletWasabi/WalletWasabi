using MagicalCryptoWallet.TorSocks5;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	// Tor must be running
    public class TorTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public TorTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public async Task CanGetTwiceAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://icanhazip.com/")))
			{
				await client.SendAsync(HttpMethod.Get, "");
				await client.SendAsync(HttpMethod.Get, "");
			}
		}

		[Fact]
		public async Task CanDoRequest1Async()
		{
			using (var client = new TorHttpClient(new Uri("http://api.qbit.ninja")))
			{
				var contents = await QBitTestAsync(client, 1);
				foreach (var content in contents)
				{
					Assert.Equal("\"Good question Holmes !\"", content);
				}
			}
		}

		[Fact]
		public async Task CanDoRequest2Async()
		{
			using (var client = new TorHttpClient(new Uri("http://api.qbit.ninja")))
			{
				var contents = await QBitTestAsync(client, 2);
				foreach (var content in contents)
				{
					Assert.Equal("\"Good question Holmes !\"", content);
				}
			}
		}

		[Fact]
		public async Task CanDoRequestManyAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://api.qbit.ninja")))
			{
				var contents = await QBitTestAsync(client, 15);
				foreach (var content in contents)
				{
					Assert.Equal("\"Good question Holmes !\"", content);
				}
			}
		}

		[Fact]
		public async Task CanDoRequestManyDifferentAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://api.qbit.ninja")))
			{
				await QBitTestAsync(client, 10, alterRequests: true);
			}
		}

		[Fact]
		public async Task CanDoHttpsRequestManyAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://api.qbit.ninja")))
			{
				var contents = await QBitTestAsync(client, 15);

				foreach (var content in contents)
				{
					Assert.Equal("\"Good question Holmes !\"", content);
				}
			}
		}

		[Fact]
		public async Task CanRequestChunkEncodedAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://jigsaw.w3.org/")))
			{
				var response = await client.SendAsync(HttpMethod.Get, "/HTTP/ChunkedScript");
				var content = await response.Content.ReadAsStringAsync();
				Assert.Equal(1000, Regex.Matches(content, "01234567890123456789012345678901234567890123456789012345678901234567890").Count);
			}
		}

		[Fact]
		public async Task TestMicrosoftNCSIAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://www.msftncsi.com/")))
			{
				var response = await client.SendAsync(HttpMethod.Get, "ncsi.txt");
				var content = await response.Content.ReadAsStringAsync();
				Assert.Equal("Microsoft NCSI", content);
			}
		}

		[Fact]
		public async Task CanDoBasicPostRequestAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://httpbin.org")))
			{
				HttpContent content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("foo", "bar@98")
				});

				HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "post", content);
				var responseContentString = await message.Content.ReadAsStringAsync();

				Assert.Contains("bar@98", responseContentString);
			}
		}

		[Fact]
		public async Task CanDoBasicPostRequestWithNonAsciiCharsAsync()
		{
			using (var client = new TorHttpClient(new Uri("http://httpbin.org")))
			{
				string json = "Hello ñ";
				var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

				HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "post", httpContent);
				var responseContentString = await message.Content.ReadAsStringAsync();

				Assert.Contains(@"Hello \u00f1", responseContentString);
			}
		}

		[Fact]
		public async Task CanDoBasicPostHttpsRequestAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://api.smartbit.com.au")))
			{
				HttpContent content = new StringContent("{\"hex\": \"01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff2d03a58605204d696e656420627920416e74506f6f6c20757361311f10b53620558903d80272a70c0000724c0600ffffffff010f9e5096000000001976a9142ef12bd2ac1416406d0e132e5bc8d0b02df3861b88ac00000000\"}");

				HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "/v1/blockchain/decodetx", content);
				var responseContentString = await message.Content.ReadAsStringAsync();

				Assert.Equal("{\"success\":true,\"transaction\":{\"Version\":\"1\",\"LockTime\":\"0\",\"Vin\":[{\"TxId\":null,\"Vout\":null,\"ScriptSig\":null,\"CoinBase\":\"03a58605204d696e656420627920416e74506f6f6c20757361311f10b53620558903d80272a70c0000724c0600\",\"TxInWitness\":null,\"Sequence\":\"4294967295\"}],\"Vout\":[{\"Value\":25.21865743,\"N\":0,\"ScriptPubKey\":{\"Asm\":\"OP_DUP OP_HASH160 2ef12bd2ac1416406d0e132e5bc8d0b02df3861b OP_EQUALVERIFY OP_CHECKSIG\",\"Hex\":\"76a9142ef12bd2ac1416406d0e132e5bc8d0b02df3861b88ac\",\"ReqSigs\":1,\"Type\":\"pubkeyhash\",\"Addresses\":[\"15HCzh8AoKRnTWMtmgAsT9TKUPrQ6oh9HQ\"]}}],\"TxId\":\"a02b9bd4264ab5d7c43ee18695141452b23b230b2a8431b28bbe446bf2b2f595\"}}", responseContentString);
			}
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
					using (var ipClient = new TorHttpClient(new Uri("https://api.ipify.org/")))
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
