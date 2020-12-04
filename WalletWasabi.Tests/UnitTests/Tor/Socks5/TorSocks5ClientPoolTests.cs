using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
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
		/// <seealso href="https://stackoverflow.com/questions/9114053/sample-on-namedpipeserverstream-vs-namedpipeserverclient-having-pipedirection-in"/>
		[Fact(Skip = "Seems to fail sometimes.")]
		public async Task TestSendingAsync()
		{
			using CancellationTokenSource timeoutCts = new(millisecondsDelay: 10_000);

			ClearnetHttpClient httpClient = new();
			TorPoolItemManager poolItemManager = new(maxPoolItemsPerHost: 2);

			using var pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut, 4);
			using var pipeClient = new NamedPipeClientStream(".", "testpipe", PipeDirection.InOut, PipeOptions.None);

			Task connectClientTask = pipeServer.WaitForConnectionAsync(timeoutCts.Token);
			await pipeClient.ConnectAsync(timeoutCts.Token);
			await connectClientTask;

			TestPoolItemFactory testPoolItemFactory = new(pipeClient);

			TorSocks5ClientPool pool = new(httpClient, poolItemManager, testPoolItemFactory.CreateNewAsync);

			// Client sending HTTP request.
			Task sendTask = Task.Run(async () =>
			{
				Debug.WriteLine("[client] About send HTTP request.");
				using HttpRequestMessage requestMessage = new(HttpMethod.Get, "http://postman-echo.com");
				using HttpResponseMessage httpResponseMessage = await pool.SendAsync(requestMessage, isolateStream: true, timeoutCts.Token);
				Debug.WriteLine("[client] Done sending HTTP request.");
			});

			using StreamReader serverReader = new(pipeServer);
			using StreamWriter serverWriter = new(pipeServer);
			Debug.WriteLine("[server] About to read data.");

			Assert.Equal("GET / HTTP/1.1", await serverReader.ReadLineAsync());
			Assert.Equal("Accept-Encoding:gzip", await serverReader.ReadLineAsync());
			Assert.Equal("Host:postman-echo.com", await serverReader.ReadLineAsync());
			string? actual = await serverReader.ReadLineAsync();
			Assert.Equal("", actual);

			Debug.WriteLine("[server] Send response for the request.");
			await serverWriter.WriteAsync(string.Join("\r\n",
				"HTTP/1.1 200 OK",
				"Date: Wed, 02 Dec 2020 18:20:54 GMT",
				"Content-Type: application/json; charset=utf-8",
				"Content-Length: 389",
				"Connection: keep-alive",
				"ETag: W/\"185-ck4yLFUDHZl9lYSDUF6oMrTCEss\"",
				"Vary: Accept-Encoding",
				"set-cookie: sails.sid=s%3AMPaQCDY1u1swPgAI5RhbPg2extVNNhjI.oby40NpOE2CpyzIdRlGhD7Uja%2BGX1WbBaFV13T0f4eA; Path=/; HttpOnly",
				"",
				"{\"args\":{},\"data\":\"This is expected to be sent back as part of response body.\",\"files\":{},\"form\":{},\"headers\":{\"x-forwarded-proto\":\"http\",\"x-forwarded-port\":\"80\",\"host\":\"postman-echo.com\",\"x-amzn-trace-id\":\"Root=1-5fc7db06-24adc2a91c86c14f2d63ea61\",\"content-length\":\"58\",\"accept-encoding\":\"gzip\",\"content-type\":\"text/plain; charset=utf-8\"},\"json\":null,\"url\":\"http://postman-echo.com/post\"}"
				).AsMemory(),
				timeoutCts.Token);
			serverWriter.Flush();

			Debug.WriteLine("[server] Wait for the sendTask to finish.");
			await sendTask;
			Debug.WriteLine("[server] Send task finished.");
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