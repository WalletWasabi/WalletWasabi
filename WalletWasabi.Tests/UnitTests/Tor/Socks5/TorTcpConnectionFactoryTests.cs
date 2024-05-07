using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Models.Messages;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5;

/// <summary>
/// Tests for <see cref="TorTcpConnectionFactory"/> class.
/// </summary>
[Collection("Serial unit tests collection")]
public class TorTcpConnectionFactoryTests
{
	private static readonly TimeSpan TimeoutLimit = TimeSpan.FromMinutes(2);

	/// <summary>
	/// <list type="bullet">
	/// <item>Client sends an HTTP request to Tor SOCKS5 endpoint.</item>
	/// <item>Test server verifies that the data received is correct.</item>
	/// <item>Test server responds with <see cref="MethodField.NoAcceptableMethods"/> to the client's handshake.</item>
	/// <item><see cref="NotSupportedException"/> is expected to be thrown on the client side.</item>
	/// </list>
	/// </summary>
	[Fact]
	public async Task AuthenticationErrorScenarioAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeoutLimit);
		CancellationToken timeoutToken = timeoutCts.Token;

		// No request is sent to the URI.
		Uri uri = new("http://postman-echo.com");
		string httpRequestHost = uri.Host;
		int httpRequestPort = 80;

		TcpListener? listener = null;

		try
		{
			// Start local TCP server.
			listener = new(IPAddress.Loopback, port: 0);
			listener.Start();
			int serverPort = ((IPEndPoint)listener.LocalEndpoint).Port;

			Logger.LogTrace($"[{nameof(AuthenticationErrorScenarioAsync)}][server] Waiting for a TCP client on port {serverPort}.");
			ValueTask<TcpClient> acceptTask = listener.AcceptTcpClientAsync(timeoutToken);

			Task clientTask = Task.Run(
				async () =>
				{
					TorTcpConnectionFactory factory = new(new IPEndPoint(IPAddress.Loopback, serverPort));

					Logger.LogTrace($"[{nameof(AuthenticationErrorScenarioAsync)}][client] About to make connection.");
					using TorTcpConnection torConnection = await factory.ConnectAsync(httpRequestHost, httpRequestPort, useSsl: false, DefaultCircuit.Instance, timeoutToken).ConfigureAwait(false);
					Logger.LogTrace($"[{nameof(AuthenticationErrorScenarioAsync)}][client] Connection established.");
				},
				timeoutToken);

			using TcpClient client = await acceptTask;

			Logger.LogTrace($"[{nameof(AuthenticationErrorScenarioAsync)}][server] Connected!");
			using NetworkStream stream = client.GetStream();
			stream.ReadTimeout = (int)TimeoutLimit.TotalMilliseconds;

			// Read SOCKS protocol version.
			int versionByte = stream.ReadByte();
			Assert.Equal(VerField.Socks5.Value, versionByte);

			// Read "NMethods" version.
			int nmethodsByte = stream.ReadByte();
			Assert.Equal(1, nmethodsByte);

			// Read SOCKS version.
			int methodByte = stream.ReadByte();
			Assert.Equal(MethodField.NoAuthenticationRequired.ToByte(), methodByte); // Default circuits use this MethodField.

			// Write response: version + method selected.
			stream.WriteByte(VerField.Socks5.Value);
			stream.WriteByte(MethodField.NoAcceptableMethods.ToByte());
			stream.Flush();

			Logger.LogTrace($"[{nameof(AuthenticationErrorScenarioAsync)}][server] Expecting exception.");
			await Assert.ThrowsAsync<NotSupportedException>(async () => await clientTask.WaitAsync(timeoutToken).ConfigureAwait(false));
		}
		finally
		{
			listener?.Dispose();
		}
	}

	/// <summary>
	/// <list type="bullet">
	/// <item>Client sends an HTTP request to Tor SOCKS5 endpoint.</item>
	/// <item>Test server verifies that the data received is correct.</item>
	/// <item>Test server responds with <see cref="RepField.TtlExpired"/> to the client's CONNECT command.</item>
	/// <item><see cref="TorConnectCommandFailedException"/> is expected to be thrown on the client side.</item>
	/// </list>
	/// </summary>
	[Fact]
	public async Task TtlExpiredScenarioAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeoutLimit);
		CancellationToken timeoutToken = timeoutCts.Token;

		Uri uri = new("http://postman-echo.com");
		string httpRequestHost = uri.Host;
		int httpRequestPort = 80;

		TcpListener? listener = null;

		try
		{
			// Use a person circuit to test MethodField being sent to Tor.
			using PersonCircuit personCircuit = new();

			// Start local TCP server.
			listener = new(IPAddress.Loopback, port: 0);
			listener.Start();
			int serverPort = ((IPEndPoint)listener.LocalEndpoint).Port;

			Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][server] Wait for TCP client on port {serverPort}.");
			ValueTask<TcpClient> acceptTask = listener.AcceptTcpClientAsync(timeoutToken);

			Task clientTask = Task.Run(
				async () =>
				{
					TorTcpConnectionFactory factory = new(new IPEndPoint(IPAddress.Loopback, serverPort));

					Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][client] About to make connection.");
					using TorTcpConnection torConnection = await factory.ConnectAsync(httpRequestHost, httpRequestPort, useSsl: false, personCircuit, timeoutToken);
					Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][client] Connection established.");
				},
				timeoutToken);

			using TcpClient client = await acceptTask;

			Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][server] Connected!");
			using NetworkStream stream = client.GetStream();
			stream.ReadTimeout = (int)TimeoutLimit.TotalMilliseconds;

			// <Version method request>
			// Read SOCKS protocol version.
			int versionByte = stream.ReadByte();
			Assert.Equal(VerField.Socks5.Value, versionByte);

			// Read "NMethods" version.
			int nmethodsByte = stream.ReadByte();
			Assert.Equal(1, nmethodsByte);

			// Read method byte.
			int methodByte = stream.ReadByte();
			Assert.Equal(MethodField.UsernamePassword.ToByte(), methodByte);

			// Write response: version + method selected.
			stream.WriteByte(VerField.Socks5.Value);
			stream.WriteByte(MethodField.UsernamePassword.ToByte());
			stream.Flush();

			// </Version method request>

			// <UsernamePasswordRequest>
			// Read "AuthVerField" byte.
			int authVerByte = stream.ReadByte();
			Assert.Equal(AuthVerField.Version1.Value, authVerByte);

			int ulenByte = stream.ReadByte();
			Assert.Equal(21, ulenByte);

			// Read "UName".
			for (int j = 0; j < 21; j++)
			{
				_ = stream.ReadByte();
			}

			int plenByte = stream.ReadByte();
			Assert.Equal(1, plenByte);
			int passwordByte = stream.ReadByte();
			Assert.Equal('0', passwordByte); // Isolation ID is equal to "0" (zero character string).

			// Write response (UsernamePasswordResponse): version + method selected.
			stream.WriteByte((byte)AuthVerField.Version1.Value);
			stream.WriteByte(AuthStatusField.Success.ToByte());
			stream.Flush();

			// </UsernamePasswordRequest>

			TorSocks5Request expectedConnectionRequest = new(cmd: CmdField.Connect, new(httpRequestHost), new(httpRequestPort));

			int i = 0;
			foreach (byte byteValue in expectedConnectionRequest.ToBytes())
			{
				i++;
				Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][server] Reading request byte #{i}.");
				int readByte = stream.ReadByte();
				Assert.Equal(byteValue, readByte);
			}

			// Tor SOCKS5 reply reporting TTL expired error.
			// Note: RepField.Succeeded is the only OK code.
			// https://tools.ietf.org/html/rfc1928: See "6. Replies"
			byte[] torSocks5Response = new byte[]
			{
					VerField.Socks5.Value,
					RepField.TtlExpired.ToByte(),
					RsvField.X00.ToByte(),
					AtypField.DomainName.ToByte(),
					0x04, 0x00, 0x00, 0x00, 0x00, // BndAddr (ATYP = "Domain" therefore the first octet is length of "BND.ADDR")
					0x00, 0x00 // BndPort
			};

			Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][server] Respond with RepField.TtlExpired result.");
			await stream.WriteAsync(torSocks5Response, timeoutToken);
			stream.Flush();

			Logger.LogTrace($"[{nameof(TtlExpiredScenarioAsync)}][server] Expecting exception.");
			await Assert.ThrowsAsync<TorConnectCommandFailedException>(async () => await clientTask.WaitAsync(timeoutToken));
		}
		finally
		{
			listener?.Dispose();
		}
	}
}
