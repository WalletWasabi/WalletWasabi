using NBitcoin;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Control;
using Xunit;
using static WalletWasabi.Tor.Control.PipeReaderLineReaderExtension;

namespace WalletWasabi.Tests.UnitTests.Tor.Control;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class TorControlFactoryTests
{
	/// <summary>
	/// Tests authenticating to Tor Control using the safe-cookie mechanism.
	/// </summary>
	[Fact]
	public async Task SafeCookieAuthenticationAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

		// Setup.
		string cookieString = "200339DD9897265DF859C6578DFB51E2E867AA8966C9112349262F5398DEFC3D";
		string clientNonce = "6F14C18D5B00BF54E16E4728A4BFC81B1FF469F0B012CD71D9724BFBE14DB5E6";
		string serverHash = "E3C00FB4A14AF48B43CE8A13E4BB01F8C72796352072B1994EE21D35148931C1";
		string serverNonce = "1650507A46A2979974DA72A833523B72789A65F6E24EAA59C5DF1D3DC294228D";

		var mockRandom = new TesteableRandom
		{
			OnGetBytes = (buffer) => Array.Copy(sourceArray: Convert.FromHexString(clientNonce), buffer, 32)
		};

		TorControlClientFactory clientFactory = new(mockRandom);

		Pipe toServer = new();
		Pipe toClient = new();

		await using TorControlClient testClient = new(TorBackend.CTor, pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

		Logger.LogTrace("Client: Start authentication task.");
		Task<TorControlClient> authenticationTask = clientFactory.AuthSafeCookieOrThrowAsync(testClient, cookieString, timeoutCts.Token);

		Logger.LogTrace("Server: Read 'AUTHCHALLENGE SAFECOOKIE' command from the client.");
		string authChallengeCommand = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token);

		Logger.LogTrace($"Server: Received AUTHCHALLENGE line: '{authChallengeCommand}'.");
		Assert.Equal("AUTHCHALLENGE SAFECOOKIE 6F14C18D5B00BF54E16E4728A4BFC81B1FF469F0B012CD71D9724BFBE14DB5E6", authChallengeCommand);

		Logger.LogTrace("Server: Respond to client's AUTHCHALLENGE request.");
		string challengeResponse = $"250 AUTHCHALLENGE SERVERHASH={serverHash} SERVERNONCE={serverNonce}\r\n";
		await toClient.Writer.WriteAsciiAndFlushAsync(challengeResponse, timeoutCts.Token);

		Logger.LogTrace("Server: Read 'AUTHENTICATE' command from the client.");
		string authCommand = await toServer.Reader.ReadLineAsync(LineEnding.CRLF, timeoutCts.Token);

		Logger.LogTrace($"Server: Received auth line: '{authCommand}'.");
		Assert.Equal("AUTHENTICATE 6013EA09D4E36B6CF01C18A707D350C1B5AFF8C1A21527266B9FC40C89BDCB4A", authCommand);

		Logger.LogTrace("Server: Respond to the client's AUTHENTICATION request.");
		await toClient.Writer.WriteAsciiAndFlushAsync("250 OK\r\n", timeoutCts.Token);

		Logger.LogTrace("Client: Verify the authentication task finishes correctly.");
		TorControlClient authenticatedClient = await authenticationTask;
		Assert.NotNull(authenticatedClient);
	}
}

class TesteableRandom : IRandom
{
	public Action<byte[]>? OnGetBytes { get; set; }

	public void GetBytes(byte[] output)
	{
		OnGetBytes?.Invoke(output);
	}

	public void GetBytes(Span<byte> output)
	{
		throw new NotImplementedException();
	}
}
