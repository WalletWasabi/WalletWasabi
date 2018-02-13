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
		public async Task CanRequestChunkEncodedAsync()
		{
			using (var client = new TorHttpClient(new Uri("https://jigsaw.w3.org/")))
			{
				var response = await client.SendAsync(HttpMethod.Get, "/HTTP/ChunkedScript");
				var content = await response.Content.ReadAsStringAsync();
				Assert.Equal(1000, Regex.Matches(content, "01234567890123456789012345678901234567890123456789012345678901234567890").Count);
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
