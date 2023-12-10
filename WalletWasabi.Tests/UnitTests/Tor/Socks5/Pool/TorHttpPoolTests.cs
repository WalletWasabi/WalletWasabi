using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5.Pool;

/// <summary>
/// Tests for <see cref="TorHttpPool"/> class.
/// </summary>
/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class TorHttpPoolTests
{
	/// <summary>
	/// Tests that <see cref="TorHttpPool.SendAsync(HttpRequestMessage, ICircuit, CancellationToken)"/> method respects provided Tor circuit.
	/// </summary>
	[Fact]
	public async Task UseCorrectIdentitiesAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

		INamedCircuit defaultCircuit = DefaultCircuit.Instance;
		using PersonCircuit aliceIdentity = new();
		using PersonCircuit bobIdentity = new();

		using TorTcpConnection aliceConnection = new(null!, new MemoryStream(), aliceIdentity, true);
		using TorTcpConnection bobConnection = new(null!, new MemoryStream(), bobIdentity, true);
		using TorTcpConnection defaultConnection = new(null!, new MemoryStream(), defaultCircuit, true);

		MockTorTcpConnectionFactory mockTcpConnectionFactory = new(new IPEndPoint(IPAddress.Loopback, 7777));
		mockTcpConnectionFactory.OnConnectAsync = (_, identity) =>
			Task.FromResult(identity switch
			{
				PersonCircuit p when p == aliceIdentity => aliceConnection,
				PersonCircuit p when p == bobIdentity => bobConnection,
				DefaultCircuit p when p == defaultCircuit => defaultConnection,
				_ => throw new InvalidOperationException("Review your test. You've ruined it.")
			});

		TorTcpConnectionFactory tcpConnectionFactory = mockTcpConnectionFactory;

		// Use implementation of TorHttpPool and only replace SendCoreAsync behavior.
		Mock<TorHttpPool> mockTorHttpPool = new(MockBehavior.Loose, tcpConnectionFactory) { CallBase = true };
		mockTorHttpPool.Setup(x => x.SendCoreAsync(It.IsAny<TorTcpConnection>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
			.Returns((TorTcpConnection tcpConnection, HttpRequestMessage request, Uri requestUriOverride, CancellationToken cancellationToken) =>
			{
				HttpResponseMessage httpResponse = new(HttpStatusCode.OK);

				if (tcpConnection == aliceConnection)
				{
					httpResponse.Content = new StringContent("Alice circuit!");
				}
				else if (tcpConnection == bobConnection)
				{
					httpResponse.Content = new StringContent("Bob circuit!");
				}
				else if (tcpConnection == defaultConnection)
				{
					httpResponse.Content = new StringContent("Default circuit!");
				}
				else
				{
					throw new NotSupportedException();
				}

				return Task.FromResult(httpResponse);
			});

		await using TorHttpPool pool = mockTorHttpPool.Object;

		using HttpRequestMessage request = new(HttpMethod.Get, "http://wasabi.backend");

		using HttpResponseMessage aliceResponse = await pool.SendAsync(request, aliceIdentity, timeoutCts.Token);
		Assert.Equal("Alice circuit!", await aliceResponse.Content.ReadAsStringAsync(timeoutCts.Token));

		using HttpResponseMessage bobResponse = await pool.SendAsync(request, bobIdentity, timeoutCts.Token);
		Assert.Equal("Bob circuit!", await bobResponse.Content.ReadAsStringAsync(timeoutCts.Token));

		using HttpResponseMessage defaultResponse = await pool.SendAsync(request, defaultCircuit, timeoutCts.Token);
		Assert.Equal("Default circuit!", await defaultResponse.Content.ReadAsStringAsync(timeoutCts.Token));
	}

	/// <summary>
	/// Tests that <see cref="TorHttpPool.SendAsync(HttpRequestMessage, ICircuit, CancellationToken)"/> bumps <see cref="INamedCircuit.IsolationId"/>
	/// on a failure.
	/// </summary>
	[Fact]
	public async Task TestIsolationIdBumpingAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

		using PersonCircuit aliceCircuit = new();

		MockTorTcpConnectionFactory mockTcpConnectionFactory = new(new IPEndPoint(IPAddress.Loopback, 7777));
		var callCount = 0;
		mockTcpConnectionFactory.OnConnectAsync = (_, identity) =>
		{
			if ((identity, callCount) == (aliceCircuit, 0))
			{
				callCount++;
				return Task.FromException<TorTcpConnection>(new TorConnectionException("Could not connect to Tor SOCKSPort."));
			}

			if ((identity, callCount) == (aliceCircuit, 1))
			{
				callCount++;
				return Task.FromException<TorTcpConnection> (new OperationCanceledException("Deadline reached."));
			}

			return Task.FromException<TorTcpConnection>(new InvalidOperationException("This shouldn't happen."));
		};



		await using TorHttpPool pool = new(mockTcpConnectionFactory);

		// HTTP request to send.
		using HttpRequestMessage request = new(HttpMethod.Get, "http://wasabi.backend");

		// Verify IsolationId bumping for the Alice circuit.
		Assert.Equal(0, aliceCircuit.IsolationId);

		await Assert.ThrowsAsync<OperationCanceledException>(() => pool.SendAsync(request, aliceCircuit, timeoutCts.Token));

		Assert.True(aliceCircuit.IsolationId > 0);
	}

	/// <summary>
	/// Tests that <see cref="TorHttpPool.SendAsync(HttpRequestMessage, ICircuit, CancellationToken)"/> method sends data as expected and
	/// when sends an HTTP reply, it is correctly processed by <see cref="TorHttpPool"/>.
	/// </summary>
	[Fact]
	public async Task RequestAndReplyAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

		INamedCircuit circuit = DefaultCircuit.Instance;

		// Set up FAKE transport stream, so Tor is not in play.
		await using TransportStream transportStream = new(nameof(RequestAndReplyAsync));
		await transportStream.ConnectAsync(timeoutCts.Token);

		using TorTcpConnection connection = new(tcpClient: null!, transportStream.Client, circuit, allowRecycling: true);

		MockTorTcpConnectionFactory mockFactory = new(new IPEndPoint(IPAddress.Loopback, 7777));
		mockFactory.OnConnectAsync = (_, _) => Task.FromResult(connection);

		using StreamReader serverReader = new(transportStream.Server);
		using StreamWriter serverWriter = new(transportStream.Server);

		await using TorHttpPool pool = new(mockFactory);
		using HttpRequestMessage request = new(HttpMethod.Get, "http://somesite.com");

		Task sendTask = Task.Run(async () =>
		{
			Debug.WriteLine("[client] About to send HTTP request.");
			using HttpResponseMessage httpResponseMessage = await pool.SendAsync(request, circuit, timeoutCts.Token).ConfigureAwait(false);
			Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
			Debug.WriteLine("[client] Done sending HTTP request.");
		});

		// Server part follows.
		Debug.WriteLine("[server] About to read data.");

		// We expect to get this plaintext HTTP request headers from the client.
		string[] expectedResponse = new[]
		{
			"GET / HTTP/1.1",
			"Accept-Encoding:gzip",
			"Host:somesite.com",
			""
		};

		// Assert replies line by line.
		foreach (string expectedLine in expectedResponse)
		{
			Assert.Equal(expectedLine, await serverReader.ReadLineAsync(timeoutCts.Token));
		}

		// We respond to the client with the following content.
		Debug.WriteLine("[server] Send response for the request.");
		await serverWriter.WriteAsync("""
			HTTP/1.1 200 OK
			Date: Wed, 02 Dec 2020 18:20:54 GMT
			Content-Type: application/json; charset=utf-8
			Content-Length: 389
			Connection: keep-alive
			ETag: W/\"185-ck4yLFUDHZl9lYSDUF6oMrTCEss\"
			Vary: Accept-Encoding
			set-cookie: sails.sid=s%3AMPaQCDY1u1swPgAI5RhbPg2extVNNhjI.oby40NpOE2CpyzIdRlGhD7Uja%2BGX1WbBaFV13T0f4eA; Path=/; HttpOnly

			{"args":{},"data":"This is expected to be sent back as part of response body.","files":{},"form":{},"headers":{"x-forwarded-proto":"http","x-forwarded-port":"80","host":"postman-echo.com","x-amzn-trace-id":"Root=1-5fc7db06-24adc2a91c86c14f2d63ea61","content-length":"58","accept-encoding":"gzip","content-type":"text/plain; charset=utf-8"},"json":null,"url":"http://postman-echo.com/post"}
			""".ReplaceLineEndings("\r\n").AsMemory(),
			timeoutCts.Token);
		await serverWriter.FlushAsync().WaitAsync(timeoutCts.Token);

		Debug.WriteLine("[server] Wait for the sendTask to finish.");
		await sendTask;
		Debug.WriteLine("[server] Send task finished.");
	}

	/// <summary>
	/// Tests that <see cref="TorHttpPool.SendAsync(HttpRequestMessage, ICircuit, CancellationToken)"/> can handle <see cref="HttpStatusCode.Found"/> (302)
	/// and redirects to the provided location.
	/// </summary>
	[Fact]
	public async Task RedirectSupportAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(4));

		// TODO: Test with OneOffCircuit (disposing?)
		INamedCircuit circuit = DefaultCircuit.Instance;

		// Set up server handling for the first HTTP request (containing 'location' HTTP header).
		await using TransportStream transportStream1 = new($"{nameof(RedirectSupportAsync)}.1");
		await transportStream1.ConnectAsync(timeoutCts.Token);
		using StreamReader serverReader1 = new(transportStream1.Server);
		using StreamWriter serverWriter1 = new(transportStream1.Server);
		using TorTcpConnection connection1 = new(tcpClient: null!, transportStream1.Client, circuit, allowRecycling: true);

		// Set up server handling for the second HTTP request (the final destination).
		await using TransportStream transportStream2 = new($"{nameof(RedirectSupportAsync)}.2");
		await transportStream2.ConnectAsync(timeoutCts.Token);
		using StreamReader serverReader2 = new(transportStream2.Server);
		using StreamWriter serverWriter2 = new(transportStream2.Server);
		using TorTcpConnection connection2 = new(tcpClient: null!, transportStream2.Client, circuit, allowRecycling: true);

		Mock<TorTcpConnectionFactory> mockFactory = new(MockBehavior.Strict, new IPEndPoint(IPAddress.Loopback, 7777));
		mockFactory.SetupSequence(c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<INamedCircuit>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(connection1)
			.ReturnsAsync(connection2);

		await using TorHttpPool pool = new(mockFactory.Object);
		using HttpRequestMessage request = new(HttpMethod.Get, "http://api.github.com/redirect/123456");

		Task sendTask = Task.Run(async () =>
		{
			Debug.WriteLine("[client] About to send HTTP request.");
			using HttpResponseMessage httpResponseMessage = await pool.SendAsync(request, circuit, timeoutCts.Token).ConfigureAwait(false);
			Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
			Debug.WriteLine("[client] Done sending HTTP request.");
		});

		Debug.WriteLine("[server] Handle the first request.");
		{
			// We expect to get this plaintext HTTP request headers from the client.
			string[] expectedServerResponse1 = new[]
			{
				"GET /redirect/123456 HTTP/1.1",
				"Accept-Encoding:gzip",
				"Host:api.github.com",
				""
			};

			// Assert server replies line by line.
			foreach (string expectedLine in expectedServerResponse1)
			{
				string? actualLine = await serverReader1.ReadLineAsync(timeoutCts.Token);
				Assert.Equal(expectedLine, actualLine);
			}

			// We respond to the client with the following content.
			Debug.WriteLine("[server] Send response for the request.");
			string serverResponse1 = """
				HTTP/1.1 302 Found
				Server: GitHub.com
				Date: Sun, 11 Dec 2022 09:47:26 GMT
				Content-Type: text/html; charset=utf-8
				Vary: X-PJAX, X-PJAX-Container, Turbo-Visit, Turbo-Frame, Accept-Encoding, Accept, X-Requested-With
				Location: https://objects.githubusercontent.com/github-production-release-asset-2e65be/55341469/84261958-b5b5-45dc-a1fe-3bd96253e120?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIWNJYAX4CSVEH53A%2F20221211%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20221211T094726Z&X-Amz-Expires=300&X-Amz-Signature=bee3014421243a64ae1d2ffc7ca5e3693cbbd49b08b386df7bd7569494d04a7f&X-Amz-SignedHeaders=host&actor_id=0&key_id=0&repo_id=55341469&response-content-disposition=attachment%3B%20filename%3DWasabi-2.0.1.4.msi&response-content-type=application%2Foctet-stream
				Cache-Control: no-cache
				Strict-Transport-Security: max-age=31536000; includeSubdomains; preload
				X-Frame-Options: deny
				X-Content-Type-Options: nosniff
				X-XSS-Protection: 0
				Referrer-Policy: no-referrer-when-downgrade
				Content-Security-Policy: default-src 'none'; base-uri 'self'; block-all-mixed-content; child-src github.com/assets-cdn/worker/ gist.github.com/assets-cdn/worker/; connect-src 'self' uploads.github.com objects-origin.githubusercontent.com www.githubstatus.com collector.github.com raw.githubusercontent.com api.github.com github-cloud.s3.amazonaws.com github-production-repository-file-5c1aeb.s3.amazonaws.com github-production-upload-manifest-file-7fdce7.s3.amazonaws.com github-production-user-asset-6210df.s3.amazonaws.com cdn.optimizely.com logx.optimizely.com/v1/events *.actions.githubusercontent.com wss://*.actions.githubusercontent.com online.visualstudio.com/api/v1/locations github-production-repository-image-32fea6.s3.amazonaws.com github-production-release-asset-2e65be.s3.amazonaws.com insights.github.com wss://alive.github.com; font-src github.githubassets.com; form-action 'self' github.com gist.github.com objects-origin.githubusercontent.com; frame-ancestors 'none'; frame-src viewscreen.githubusercontent.com notebooks.githubusercontent.com; img-src 'self' data: github.githubassets.com media.githubusercontent.com camo.githubusercontent.com identicons.github.com avatars.githubusercontent.com github-cloud.s3.amazonaws.com objects.githubusercontent.com objects-origin.githubusercontent.com secured-user-images.githubusercontent.com/ opengraph.githubassets.com github-production-user-asset-6210df.s3.amazonaws.com customer-stories-feed.github.com spotlights-feed.github.com *.githubusercontent.com; manifest-src 'self'; media-src github.com user-images.githubusercontent.com/ secured-user-images.githubusercontent.com/; script-src github.githubassets.com; style-src 'unsafe-inline' github.githubassets.com; worker-src github.com/assets-cdn/worker/ gist.github.com/assets-cdn/worker/
				Content-Length: 0
				X-GitHub-Request-Id: AFCA:0EE6:208343C:21F2436:6395A726


				""".ReplaceLineEndings("\r\n");

			await serverWriter1.WriteAsync(serverResponse1.AsMemory(), timeoutCts.Token);
			await serverWriter1.FlushAsync().WaitAsync(timeoutCts.Token);
			serverWriter1.Close();
		}

		Debug.WriteLine("[server] Handle second request.");
		{
			// We expect to get this plaintext HTTP request headers from the client.
			string[] expectedServerResponse2 = new[]
			{
				"GET /github-production-release-asset-2e65be/55341469/84261958-b5b5-45dc-a1fe-3bd96253e120?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIWNJYAX4CSVEH53A%2F20221211%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20221211T094726Z&X-Amz-Expires=300&X-Amz-Signature=bee3014421243a64ae1d2ffc7ca5e3693cbbd49b08b386df7bd7569494d04a7f&X-Amz-SignedHeaders=host&actor_id=0&key_id=0&repo_id=55341469&response-content-disposition=attachment%3B%20filename%3DWasabi-2.0.1.4.msi&response-content-type=application%2Foctet-stream HTTP/1.1",
				"Accept-Encoding:gzip",
				"Host:objects.githubusercontent.com",
				""
			};

			// Assert server replies line by line.
			foreach (string expectedLine in expectedServerResponse2)
			{
				string? actualLine = await serverReader2.ReadLineAsync(timeoutCts.Token);
				Assert.Equal(expectedLine, actualLine);
			}

			// We respond to the client with the following content.
			Debug.WriteLine("[server] Send response for the request.");
			string serverResponse2 = """
				HTTP/1.1 200 OK
				Server: GitHub.com
				Date: Sun, 11 Dec 2022 09:47:26 GMT
				Content-Type: text/html; charset=utf-8
				Cache-Control: no-cache
				Content-Length: 30

				You got here. Congratulations.
				""".ReplaceLineEndings("\r\n");

			await serverWriter2.WriteAsync(serverResponse2.AsMemory(), timeoutCts.Token);
			await serverWriter2.FlushAsync().WaitAsync(timeoutCts.Token);
			serverWriter2.Close();
		}

		// Make sure that the original HTTP request object was not changed.
		Assert.Equal(new Uri("http://api.github.com/redirect/123456"), request.RequestUri);

		Debug.WriteLine("[server] Wait for the sendTask to finish.");
		await sendTask;
		Debug.WriteLine("[server] Send task finished.");
	}

	/// <summary>
	/// Tests that <see cref="TorHttpPool.PrebuildCircuitsUpfront(Uri, int, TimeSpan)"/> method creates
	/// the correct number of Tor circuits.
	/// </summary>
	[Fact]
	public async Task PreBuildingAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

		INamedCircuit circuit = DefaultCircuit.Instance;
		using TorTcpConnection connection = new(tcpClient: null!, transportStream: null!, circuit, allowRecycling: true);

		MockTorTcpConnectionFactory mockFactory = new(new IPEndPoint(IPAddress.Loopback, 7777));
		mockFactory.OnConnectAsync = (_, _) => Task.FromResult(connection);

		await using TorHttpPool pool = new(mockFactory);
		pool.PrebuildCircuitsUpfront(new Uri("http://walletwasabi.io"), count: 3, deadline: TimeSpan.FromSeconds(3));

		await Task.Delay(5_000, timeoutCts.Token);
	}

	/// <summary>
	/// Tests that once <see cref="PersonCircuit"/> is disposed, it cannot be used to send a new HTTP(s) request.
	/// </summary>
	[Fact]
	public async Task PersonCircuitLifetimeAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

		using PersonCircuit aliceCircuit = new();
		using TorTcpConnection aliceConnection = new(null!, new MemoryStream(), aliceCircuit, true);

		Mock<TorTcpConnectionFactory> mockTcpConnectionFactory = new(MockBehavior.Strict, new IPEndPoint(IPAddress.Loopback, 7777));
		_ = mockTcpConnectionFactory.Setup(c => c.ConnectAsync(It.IsAny<Uri>(), aliceCircuit, It.IsAny<CancellationToken>())).ReturnsAsync(aliceConnection);

		Mock<TorHttpPool> mockTorHttpPool = new(MockBehavior.Loose, mockTcpConnectionFactory.Object) { CallBase = true };
		mockTorHttpPool.Setup(x => x.SendCoreAsync(It.IsAny<TorTcpConnection>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
			.Returns((TorTcpConnection tcpConnection, HttpRequestMessage request, Uri requestUriOverride, CancellationToken cancellationToken) =>
			{
				if (tcpConnection == aliceConnection)
				{
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
				}

				throw new NotSupportedException();
			});

		await using TorHttpPool pool = mockTorHttpPool.Object;
		using HttpRequestMessage request = new(HttpMethod.Get, "http://wasabi.backend");

		// Alice circuit is NOT yet disposed.
		using HttpResponseMessage aliceResponse = await pool.SendAsync(request, aliceCircuit, timeoutCts.Token);
		Assert.True(aliceResponse.IsSuccessStatusCode);

		// Dispose Alice circuit.
		aliceCircuit.Dispose();

		// Alice circuit is already disposed and thus it cannot be used.
		HttpRequestException httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await pool.SendAsync(request, aliceCircuit, timeoutCts.Token).ConfigureAwait(false));
		_ = Assert.IsType<TorCircuitExpiredException>(httpRequestException.InnerException);
	}

	/// <summary>
	/// Transport stream implementation that behaves similarly to <see cref="NetworkStream"/>.
	/// <para>Writer can write to the stream multiple times, reader can read the written data.</para>
	/// </summary>
	/// <remarks>
	/// <see cref="MemoryStream"/> is not easy to use as a replacement for <see cref="NetworkStream"/> as we would need to use seek operation.
	/// </remarks>
	internal class TransportStream : IAsyncDisposable
	{
		public TransportStream(string testName)
		{
			// Construct unique pipe name.
			int n = Random.Shared.Next(0, 1_000_000);
			string pipeName = $"{testName}.Pipe.{n}";

			Server = new(pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
			Client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
		}

		public NamedPipeServerStream Server { get; }
		public NamedPipeClientStream Client { get; }

		public async Task ConnectAsync(CancellationToken cancellationToken)
		{
			Task connectClientTask = Server.WaitForConnectionAsync(cancellationToken);
			await Client.ConnectAsync(cancellationToken).ConfigureAwait(false);
			await connectClientTask.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		public async ValueTask DisposeAsync()
		{
			await Server.DisposeAsync();
			await Client.DisposeAsync();
		}
	}
}

public class MockTorTcpConnectionFactory : TorTcpConnectionFactory
{
	public MockTorTcpConnectionFactory(EndPoint endPoint) : base(endPoint)
	{
	}

	public Func<Uri, INamedCircuit, Task<TorTcpConnection>>? OnConnectAsync { get; set; }
	public Func<Task<bool>>? OnIsTorRunningAsync { get; set; }

	public override Task<TorTcpConnection> ConnectAsync(Uri requestUri, INamedCircuit circuit, CancellationToken cancellationToken) =>
		OnConnectAsync?.Invoke(requestUri, circuit)
		       ?? throw new NotImplementedException($"{nameof(ConnectAsync)} wa invoked but never assigned.");

	public override Task<bool> IsTorRunningAsync(CancellationToken cancellationToken) =>
		OnIsTorRunningAsync?.Invoke()
		       ?? throw new NotImplementedException($"{nameof(IsTorRunningAsync)} wa invoked but never assigned.");
}
