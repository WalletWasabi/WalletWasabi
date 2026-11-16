using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tor.Control.Exceptions;

namespace WalletWasabi.Tor.Control;

/// <summary>
/// Class to authenticate to Tor Control.
/// </summary>
public partial class TorControlClientFactory(RandomnessProvider random)
{
	/// <summary>Client HMAC-SHA256 key for AUTHCHALLENGE.</summary>
	/// <remarks>Server's HMAC key is <c>Tor safe cookie authentication server-to-controller hash</c></remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">Section 3.24. AUTHCHALLENGE</seealso>
	private static byte[] ClientHmacKey = "Tor safe cookie authentication controller-to-server hash"u8.ToArray();

	/// <summary>Helps generate nonces for AUTH challenges.</summary>
	private readonly RandomnessProvider _random = random;

	/// <summary>Connects to Tor Control endpoint and authenticates using safe-cookie mechanism.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section 3.5</seealso>
	/// <exception cref="TorControlException">If TCP connection cannot be established OR if authentication fails for some reason.</exception>
	public async Task<TorControlClient> ConnectAndAuthenticateAsync(EndPoint endPoint, string cookieString, CancellationToken cancellationToken)
	{
		TcpClient tcpClient;
		try
		{
			tcpClient = await TcpClientConnector.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Logger.LogError($"Failed to connect to the Tor control: '{endPoint}'.", e);
			throw new TorControlException($"Failed to connect to the Tor control: '{endPoint}'.", e);
		}
		TorControlClient? clientToDispose = null;

		try
		{
			TorControlClient controlClient = clientToDispose = new(tcpClient);

			await AuthSafeCookieOrThrowAsync(controlClient, cookieString, cancellationToken).ConfigureAwait(false);

			// All good, do not dispose.
			clientToDispose = null;

			return controlClient;
		}
		finally
		{
			// `!=` instead `is not null` to avoid CA2000. The analyzer should be fixed over time.
			// https://github.com/dotnet/roslyn-analyzers/issues/4981
			if (clientToDispose != null)
			{
				await clientToDispose.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>Authenticates client using SAFE-COOKIE.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section 3.24 for SAFECOOKIE authentication.</seealso>
	/// <seealso href="https://github.com/torproject/stem/blob/63a476056017dda5ede35efc4e4f7acfcc1d7d1a/stem/connection.py#L893">Python implementation.</seealso>
	/// <exception cref="TorControlException">If authentication fails for some reason.</exception>
	internal async Task<TorControlClient> AuthSafeCookieOrThrowAsync(TorControlClient controlClient, string cookieString, CancellationToken cancellationToken)
	{
		Span<byte> nonceBytes = stackalloc byte[32];
		_random(nonceBytes);
		var clientNonce = Convert.ToHexString(nonceBytes);

		var authChallengeReply = await controlClient.SendCommandAsync($"AUTHCHALLENGE SAFECOOKIE {clientNonce}\r\n", cancellationToken).ConfigureAwait(false);

		if (!authChallengeReply)
		{
			Logger.LogError($"Received invalid reply for our AUTHCHALLENGE: '{authChallengeReply}'");
			throw new TorControlException("Invalid status code in AUTHCHALLENGE reply.");
		}

		if (authChallengeReply.ResponseLines.Count != 1)
		{
			Logger.LogError($"Invalid reply: '{authChallengeReply}'");
			throw new TorControlException("Invalid number of lines in AUTHCHALLENGE reply.");
		}

		var reply = authChallengeReply.ResponseLines[0];
		var match = AuthChallengeRegex().Match(reply);

		if (!match.Success)
		{
			Logger.LogError($"Invalid reply: '{reply}'");
			throw new TorControlException("AUTHCHALLENGE reply cannot be parsed.");
		}

		var serverNonce = match.Groups[2].Value;
		var toHash = $"{cookieString}{clientNonce}{serverNonce}";

		using var hmacSha256 = new HMACSHA256(ClientHmacKey);
		var serverHash = hmacSha256.ComputeHash(Convert.FromHexString(toHash));
		var serverHashStr = Convert.ToHexString(serverHash);

		Logger.LogTrace($"Authenticate using server hash: '{serverHashStr}'.");
		var authenticationReply = await controlClient.SendCommandAsync($"AUTHENTICATE {serverHashStr}\r\n", cancellationToken).ConfigureAwait(false);

		if (!authenticationReply)
		{
			Logger.LogError($"Invalid reply: '{authenticationReply}'");
			throw new TorControlException("Invalid status in AUTHENTICATE reply.");
		}

		return controlClient;
	}

	[GeneratedRegex("^AUTHCHALLENGE SERVERHASH=([a-fA-F0-9]+) SERVERNONCE=([a-fA-F0-9]+)$")]
	private static partial Regex AuthChallengeRegex();
}
