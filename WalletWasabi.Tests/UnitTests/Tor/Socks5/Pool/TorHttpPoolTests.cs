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
		mockTorHttpPool.Setup(x => x.SendCoreAsync(It.IsAny<TorTcpConnection>(), It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.Returns((TorTcpConnection tcpConnection, HttpRequestMessage request, CancellationToken cancellationToken) =>
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

		PersonCircuit aliceCircuit = new();
		using TorTcpConnection aliceConnection = new(null!, new MemoryStream(), aliceCircuit, true);

		MockTorTcpConnectionFactory mockTcpConnectionFactory = new(new IPEndPoint(IPAddress.Loopback, 7777));
		mockTcpConnectionFactory.OnConnectAsync = (_, _) => Task.FromResult(aliceConnection);

		Mock<TorHttpPool> mockTorHttpPool = new(MockBehavior.Loose, mockTcpConnectionFactory) { CallBase = true };
		mockTorHttpPool.Setup(x => x.SendCoreAsync(It.IsAny<TorTcpConnection>(), It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.Returns((TorTcpConnection tcpConnection, HttpRequestMessage request, CancellationToken cancellationToken) =>
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
