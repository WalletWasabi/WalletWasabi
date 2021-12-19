using NBitcoin;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control;

/// <summary>
/// Class to authenticate to Tor Control.
/// </summary>
public class TorControlClientFactory
{
	private static readonly Regex AuthChallengeRegex = new($"^AUTHCHALLENGE SERVERHASH=([a-fA-F0-9]+) SERVERNONCE=([a-fA-F0-9]+)$", RegexOptions.Compiled);

	/// <summary>Client HMAC-SHA256 key for AUTHCHALLENGE.</summary>
	/// <remarks>Server's HMAC key is <c>Tor safe cookie authentication server-to-controller hash</c></remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">Section 3.24. AUTHCHALLENGE</seealso>
	private static byte[] ClientHmacKey = Encoding.ASCII.GetBytes("Tor safe cookie authentication controller-to-server hash");

	public TorControlClientFactory(IRandom? random = null)
	{
		Random = random ?? new InsecureRandom();
	}

	/// <summary>Helps generate nonces for AUTH challenges.</summary>
	private IRandom Random { get; }

	/// <summary>Connects to Tor Control endpoint and authenticates using safe-cookie mechanism.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section 3.5</seealso>
	/// <exception cref="TorControlException">If TCP connection cannot be established OR if authentication fails for some reason.</exception>
	public async Task<TorControlClient> ConnectAndAuthenticateAsync(IPEndPoint endPoint, string cookieString, CancellationToken cancellationToken = default)
	{
		TcpClient tcpClient = Connect(endPoint);
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
	internal async Task<TorControlClient> AuthSafeCookieOrThrowAsync(TorControlClient controlClient, string cookieString, CancellationToken cancellationToken = default)
	{
		byte[] nonceBytes = new byte[32];
		Random.GetBytes(nonceBytes);
		string clientNonce = ByteHelpers.ToHex(nonceBytes);

		TorControlReply authChallengeReply = await controlClient.SendCommandAsync($"AUTHCHALLENGE SAFECOOKIE {clientNonce}\r\n", cancellationToken).ConfigureAwait(false);

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

		string reply = authChallengeReply.ResponseLines[0];
		Match match = AuthChallengeRegex.Match(reply);

		if (!match.Success)
		{
			Logger.LogError($"Invalid reply: '{reply}'");
			throw new TorControlException("AUTHCHALLENGE reply cannot be parsed.");
		}

		string serverNonce = match.Groups[2].Value;
		string toHash = $"{cookieString}{clientNonce}{serverNonce}";

		using HMACSHA256 hmacSha256 = new(ClientHmacKey);
		byte[] serverHash = hmacSha256.ComputeHash(ByteHelpers.FromHex(toHash));
		string serverHashStr = ByteHelpers.ToHex(serverHash);

		Logger.LogTrace($"Authenticate using server hash: '{serverHashStr}'.");
		TorControlReply authenticationReply = await controlClient.SendCommandAsync($"AUTHENTICATE {serverHashStr}\r\n", cancellationToken).ConfigureAwait(false);

		if (!authenticationReply)
		{
			Logger.LogError($"Invalid reply: '{authenticationReply}'");
			throw new TorControlException("Invalid status in AUTHENTICATE reply.");
		}

		return controlClient;
	}

	/// <summary>
	/// Connects to Tor control using a TCP client.
	/// </summary>
	private TcpClient Connect(IPEndPoint endPoint)
	{
		try
		{
			TcpClient tcpClient = new();
			tcpClient.Connect(endPoint);
			return tcpClient;
		}
		catch (Exception e)
		{
			Logger.LogError($"Failed to connect to the Tor control: '{endPoint}'.", e);
			throw new TorControlException($"Failed to connect to the Tor control: '{endPoint}'.", e);
		}
	}
}
