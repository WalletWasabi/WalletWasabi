using NBitcoin.Crypto;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class TorTests : IAsyncLifetime
{
	public TorTests()
	{
		TorHttpPool = new(new TorTcpConnectionFactory(Common.TorSocks5Endpoint));
		TorProcessManager = new(Common.TorSettings);
	}

	private TorHttpPool TorHttpPool { get; }
	private TorProcessManager TorProcessManager { get; }

	public async Task InitializeAsync()
	{
		using CancellationTokenSource startTimeoutCts = new(TimeSpan.FromMinutes(2));

		await TorProcessManager.StartAsync(startTimeoutCts.Token).ConfigureAwait(false);
	}

	public async Task DisposeAsync()
	{
		await TorHttpPool.DisposeAsync().ConfigureAwait(false);
		await TorProcessManager.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task CanDoRequestManyDifferentAsync()
	{
		TorHttpClient client = MakeTorHttpClient(new Uri("http://api.qbit.ninja"));
		await QBitTestAsync(client, 10, alterRequests: true);

		async Task<List<string>> QBitTestAsync(TorHttpClient client, int times, bool alterRequests = false)
		{
			using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

			var relativeUri = "/whatisit/what%20is%20my%20future";

			List<Task<HttpResponseMessage>> tasks = new();
			for (var i = 0; i < times; i++)
			{
				var task = client.SendAsync(HttpMethod.Get, relativeUri);
				if (alterRequests)
				{
					TorHttpClient ipClient = MakeTorHttpClient(new Uri("https://api.ipify.org/"));
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
	}

	[Fact]
	public async Task CanRequestChunkEncodedAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient(new Uri("http://anglesharp.azurewebsites.net/"));
		var response = await client.SendAsync(HttpMethod.Get, "Chunked", null, ctsTimeout.Token);
		var content = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
		Assert.Contains("Chunked transfer encoding test", content);
		Assert.Contains("This is a chunked response after 100 ms.", content);
		Assert.Contains("This is a chunked response after 1 second. The server should not close the stream before all chunks are sent to a client.", content);
	}

	/// <remarks>
	/// <code>
	/// Expected JSON is similar to this one:
	/// {
	///   "args": {},
	///   "data": "This is expected to be sent back as part of response body.",
	///   "files": {},
	///   "form": {},
	///   "headers": {
	///       "x-forwarded-proto": "https",
	///       "x-forwarded-port": "443",
	///       "host": "postman-echo.com",
	///       "x-amzn-trace-id": "Root=1-64c68226-6e22435b2d31bee960c16236",
	///       "content-length": "58",
	///       "accept-encoding": "gzip",
	///       "content-type": "text/plain; charset=utf-8"
	///   },
	///   "json": null,
	///   "url": "https://postman-echo.com/post"
	/// }
	/// </code>
	/// </remarks>
	[Fact]
	public async Task CanDoBasicPostHttpsRequestAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient(new Uri("https://postman-echo.com"));
		using HttpContent content = new StringContent("This is expected to be sent back as part of response body.");

		using HttpResponseMessage message = await client.SendAsync(HttpMethod.Post, "post", content, ctsTimeout.Token);
		string response = await message.Content.ReadAsStringAsync(ctsTimeout.Token);

		JsonNode? responseNode = JsonNode.Parse(response);
		Assert.NotNull(responseNode);

		Assert.Equal("{}", GetJsonNode(responseNode, "args").ToJsonString());
		Assert.Equal("This is expected to be sent back as part of response body.", GetJsonNode(responseNode, "data").GetValue<string>());
		Assert.Equal("{}", GetJsonNode(responseNode, "files").ToJsonString());
		Assert.Equal("{}", GetJsonNode(responseNode, "form").ToJsonString());

		// Check headers.
		{
			JsonNode? headersNode = responseNode["headers"];
			Assert.NotNull(headersNode);

			Assert.Equal("58", GetJsonNode(headersNode, "content-length").GetValue<string>());
			Assert.Equal("gzip", GetJsonNode(headersNode, "accept-encoding").GetValue<string>());
			Assert.Equal("text/plain; charset=utf-8", GetJsonNode(headersNode, "content-type").GetValue<string>());
		}

		Assert.Null(responseNode["json"]);
		Assert.Equal("https://postman-echo.com/post", GetJsonNode(responseNode, "url").GetValue<string>());
	}

	/// <summary>
	/// Downloads a binary file and verifies its checksum to prove it's correctly downloaded.
	/// </summary>
	[Fact]
	public async Task DownloadBinaryFileAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient(new Uri("https://wasabiwallet.io"));

		using HttpRequestMessage request = new(HttpMethod.Get, "https://wasabiwallet.io/bitcoin-whitepaper.pdf");
		HttpResponseMessage response = await client.SendAsync(request, ctsTimeout.Token);
		byte[] content = await response.Content.ReadAsByteArrayAsync(ctsTimeout.Token);

		string expectedHex = "B1674191A88EC5CDD733E4240A81803105DC412D6C6708D53AB94FC248F4F553";
		string actualHex = Convert.ToHexString(Hashes.SHA256(content));

		Assert.Equal(expectedHex, actualHex);
	}

	[Fact]
	public async Task TorIpIsNotTheRealOneAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		string requestUri = "https://api.ipify.org/";
		IPAddress? realIp;
		IPAddress? torIp;

		// 1. Get the real IP.
		using (HttpClient httpClient = new())
		{
			using HttpResponseMessage response = await httpClient.GetAsync(requestUri, ctsTimeout.Token);
			string content = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
			bool gotIp = IPAddress.TryParse(content.Replace("\n", ""), out realIp);
			Assert.True(gotIp);
		}

		// 2. Get the Tor IP.
		{
			TorHttpClient torClient = MakeTorHttpClient();

			using HttpRequestMessage request = new(HttpMethod.Get, requestUri: requestUri);
			using HttpResponseMessage response = await torClient.SendAsync(request, ctsTimeout.Token);
			string content = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
			bool gotIp = IPAddress.TryParse(content.Replace("\n", ""), out torIp);
			Assert.True(gotIp);
		}

		Assert.NotEqual(realIp, torIp);
	}

	/// <remarks>
	/// Expected JSON is similar to this one:
	/// <code>
	/// {
	///  "args": {
	///    "foo1": "bar1",
	///    "foo2": "bar2"
	///  },
	///  "headers": {
	///    "x-forwarded-proto": "https",
	///    "x-forwarded-port": "443",
	///    "host": "postman-echo.com",
	///    "x-amzn-trace-id": "Root=1-64c75932-215a29396ad8c37251f3c45e",
	///    "accept-encoding": "gzip"
	///  },
	///  "url": "https://postman-echo.com/get?foo1=bar1&amp;foo2=bar2"
	/// }
	/// </code>
	/// </remarks>
	[Fact]
	public async Task CanDoHttpsAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient();

		using HttpRequestMessage request = new(HttpMethod.Get, requestUri: "https://postman-echo.com/get?foo1=bar1&foo2=bar2");
		using HttpResponseMessage response = await client.SendAsync(request, ctsTimeout.Token);
		string jsonResponse = await response.Content.ReadAsStringAsync(ctsTimeout.Token);

		JsonNode? responseNode = JsonNode.Parse(jsonResponse);
		Assert.NotNull(responseNode);

		Assert.Equal("""{"foo1":"bar1","foo2":"bar2"}""", GetJsonNode(responseNode, "args").ToJsonString());

		// Check headers.
		{
			JsonNode? headersNode = responseNode["headers"];
			Assert.NotNull(headersNode);

			Assert.Equal("https", GetJsonNode(headersNode, "x-forwarded-proto").GetValue<string>());
			Assert.Equal("443", GetJsonNode(headersNode, "x-forwarded-port").GetValue<string>());
			Assert.Equal("gzip", GetJsonNode(headersNode, "accept-encoding").GetValue<string>());
		}

		Assert.Equal("https://postman-echo.com/get?foo1=bar1&foo2=bar2", GetJsonNode(responseNode, "url").GetValue<string>());
	}

	[Fact]
	public async Task CanDoIpAddressAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient();

		// 204.8.99.144 ~ torproject.org (US).
		using HttpRequestMessage request = new(HttpMethod.Get, requestUri: "http://204.8.99.144");
		using HttpResponseMessage response = await client.SendAsync(request, ctsTimeout.Token);
		string html = await response.Content.ReadAsStringAsync(ctsTimeout.Token);

		Assert.Contains("Tor Project", html);
	}

	[Fact]
	public async Task CanRequestInRowAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient(new Uri("https://blockchain.info"));

		// 1st request.
		{
			using HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "/tx/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json", null, ctsTimeout.Token);
			string json = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
			Assert.NotEmpty(json);
		}

		// 2nd request.
		{
			using HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "/rawaddr/1ADSb1ZZ9k3NsDf3JTQCQ4mb8bthiAN6NJ", null, ctsTimeout.Token);
			string json = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
			Assert.NotEmpty(json);
		}

		// 3rd request.
		{
			using HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "/tx/69b98a4476767e6fc40f8c33b3aec7fe83b7a7d3f8c7e92203b00c6be5afbdb3?format=json", null, ctsTimeout.Token);
			string json = await response.Content.ReadAsStringAsync(ctsTimeout.Token);
			Assert.NotEmpty(json);
		}
	}

	[Fact]
	public async Task CanRequestOnionV3Async()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient client = MakeTorHttpClient();

		using HttpRequestMessage request = new(HttpMethod.Get, requestUri: "http://www.dds6qkxpwdeubwucdiaord2xgbbeyds25rbsgr73tbfpqpt4a6vjwsyd.onion");
		using HttpResponseMessage response = await client.SendAsync(request, ctsTimeout.Token);
		string content = await response.Content.ReadAsStringAsync(ctsTimeout.Token);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("whonix", content, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task DoesntIsolateStreamsAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorHttpClient c1 = MakeTorHttpClient(new Uri("http://api.ipify.org"));
		TorHttpClient c2 = MakeTorHttpClient(new Uri("http://api.ipify.org"));
		TorHttpClient c3 = MakeTorHttpClient(new Uri("http://api.ipify.org"));
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

		Assert.True(ips.Count >= 2); // Very rarely it fails to isolate.
	}

	[Fact]
	public async Task TorRunningAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(2));

		TorTcpConnectionFactory client1 = new(new IPEndPoint(IPAddress.Loopback, 37150));
		Assert.True(await client1.IsTorRunningAsync(ctsTimeout.Token));

		TorTcpConnectionFactory client2 = new(new IPEndPoint(IPAddress.Loopback, 9054));
		Assert.False(await client2.IsTorRunningAsync(ctsTimeout.Token));
	}

	private TorHttpClient MakeTorHttpClient(Mode mode = Mode.DefaultCircuit)
		=> new(new Uri("http://wasabi.local"), TorHttpPool, mode);

	private TorHttpClient MakeTorHttpClient(Uri uri, Mode mode = Mode.DefaultCircuit)
		=> new(uri, TorHttpPool, mode);

	private static JsonNode GetJsonNode(JsonNode node, string propertyName)
	{
		JsonNode? result = node[propertyName];
		Assert.NotNull(result);
		return result;
	}
}
