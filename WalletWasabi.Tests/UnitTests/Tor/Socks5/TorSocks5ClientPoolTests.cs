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
using System.Net.Sockets;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5
{
	/// <summary>
	/// Tests for <see cref="TorSocks5ClientPool"/>
	/// </summary>
	/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
	[Collection("Serial unit tests collection")]
	public class TorSocks5ClientPoolTests
	{
		/// <summary>
		/// Tests <see cref="TorSocks5ClientPool.SendAsync(HttpRequestMessage, bool, CancellationToken)"/> method.
		/// <summary>
		/// <seealso href="https://stackoverflow.com/questions/9114053/sample-on-namedpipeserverstream-vs-namedpipeserverclient-having-pipedirection-in"/>
		[Fact]
		public async Task TestSendingAsync()
		{
			// Maximum time the test can run.
			using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));
			CancellationToken timeoutToken = timeoutCts.Token;

			// Set up FAKE transport stream, so Tor is not in play.
			await using TransportStream transportStream = new(nameof(TestSendingAsync));
			await transportStream.ConnectAsync(timeoutToken);

			using StreamReader serverReader = new(transportStream.Server);
			using StreamWriter serverWriter = new(transportStream.Server);

			// Create tested class.
			using TorSocks5ClientPool pool = MakePool(transportStream.Client);

			// Client part.
			Task sendTask = Task.Run(async () =>
			{
				Debug.WriteLine("[client] About send HTTP request.");
				using HttpRequestMessage requestMessage = new(HttpMethod.Get, "http://postman-echo.com");
				using HttpResponseMessage httpResponseMessage = await pool.SendAsync(requestMessage, isolateStream: true, timeoutToken);
				Debug.WriteLine("[client] Done sending HTTP request.");
			});

			// Server part follows.
			Debug.WriteLine("[server] About to read data.");

			// We expect to get this plaintext HTTP request headers from the client.
			string[] expectedResponse = new[] {
				"GET / HTTP/1.1",
				"Accept-Encoding:gzip",
				"Host:postman-echo.com",
				""
			};

			// Assert replies line by line.
			foreach (string expectedLine in expectedResponse)
			{
				Assert.Equal(expectedLine, await serverReader.ReadLineAsync().WithAwaitCancellationAsync(timeoutToken));
			}

			// We respond to the client with the following content.
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
				timeoutToken).WithAwaitCancellationAsync(timeoutToken);
			await serverWriter.FlushAsync().WithAwaitCancellationAsync(timeoutToken);

			Debug.WriteLine("[server] Wait for the sendTask to finish.");
			await sendTask;
			Debug.WriteLine("[server] Send task finished.");
		}

		/// <summary>
		/// Sets up <see cref="TorSocks5ClientPool"/> instance with a custom transport stream.
		/// </summary>
		/// <param name="transportStream"></param>
		/// <returns></returns>
		private static TorSocks5ClientPool MakePool(Stream transportStream)
		{
			ClearnetHttpClient httpClient = new();
			TorPoolItemManager poolItemManager = new(maxPoolItemsPerHost: 2);
			TestPoolItemFactory testPoolItemFactory = new(transportStream);
			TorSocks5ClientPool pool = new(httpClient, poolItemManager, testPoolItemFactory.CreateNewAsync);
			return pool;
		}
	}

	/// <summary>
	/// Transport stream implementation that behaves similarly to <see cref="NetworkStream"/>.
	/// <para>Writer can write to the stream multiple times, reader can read the written data.</para>
	/// </summary>
	/// <remarks>
	/// <see cref="MemoryStream"/> is not easy to use as a replacement for <see cref="NetworkStream"/> as we would need to use seek operation.
	/// </remarks>
	public class TransportStream : IAsyncDisposable
	{
		public TransportStream(string testName)
		{
			// Construct unique pipe name.
			int n = new Random().Next(0, 1_000_000);
			string pipeName = $"{testName}.Pipe.{n}";

			Server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
			Client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
		}

		public async Task ConnectAsync(CancellationToken cancellationToken)
		{
			Task connectClientTask = Server.WaitForConnectionAsync(cancellationToken);
			await Client.ConnectAsync(cancellationToken);
			await connectClientTask;
		}

		public NamedPipeServerStream Server { get; }
		public NamedPipeClientStream Client { get; }

		public async ValueTask DisposeAsync()
		{
			await Server.DisposeAsync();
			await Client.DisposeAsync();
		}
	}

	/// <summary>
	/// Factory for <see cref="IPoolItem"/>s where each new pool item gets a transport stream of our choosing.
	/// </summary>
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