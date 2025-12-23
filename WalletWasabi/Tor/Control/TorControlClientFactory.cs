using NBitcoin;
using Org.BouncyCastle.Crypto.Digests;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Json;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control;

/// <summary>
/// Class to authenticate to Tor Control.
/// </summary>
public partial class TorControlClientFactory
{
	/// <summary>Customization bytes used by Arti when hashing using TupleHash.</summary>
	/// <seealso href="https://gitlab.torproject.org/tpo/core/arti/-/blob/main/doc/dev/rpc-book/src/rpc-cookie-spec.md#preliminaries"/>
	private static byte[] TupleHashCustomization = Encoding.ASCII.GetBytes("arti-rpc-cookie-v1");

	public TorControlClientFactory(IRandom? random = null)
	{
		_random = random ?? new UnsecureRandom();
	}

	/// <summary>Helps generate nonces for AUTH challenges.</summary>
	private readonly IRandom _random;

	/// <summary>Connects to Tor Control endpoint and authenticates using safe-cookie mechanism.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section 3.5</seealso>
	/// <exception cref="TorControlException">If TCP connection cannot be established OR if authentication fails for some reason.</exception>
	public async Task<TorControlClient> ConnectAndAuthenticateAsync(EndPoint endPoint, string? cookieString, CancellationToken cancellationToken)
	{
		NetworkStream socket;
		try
		{
			socket = await TcpClientConnector.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Logger.LogError($"Failed to connect to the Tor control: '{endPoint}'.", e);
			throw new TorControlException($"Failed to connect to the Tor control: '{endPoint}'.", e);
		}
		TorControlClient? clientToDispose = null;

		try
		{
			TorControlClient controlClient = clientToDispose = new(socket);
			await AuthOrThrowAsync(controlClient, cookieString, cancellationToken).ConfigureAwait(false);

			// All good, do not dispose.
			clientToDispose = null;

			return controlClient;
		}
		catch (Exception e)
		{
			Logger.LogError("Cookie authentication failed.", e);
			throw;
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
	internal async Task<TorControlClient> AuthOrThrowAsync(TorControlClient controlClient, string? cookieString, CancellationToken cancellationToken)
	{
		byte[] nonceBytes = new byte[32];
		_random.GetBytes(nonceBytes);
		string clientNonce = Convert.ToHexString(nonceBytes);

		TorControlReply authenticationReply;

		// Cookie authentication.
		if (cookieString is not null)
		{
			var json = $$"""{"id": 1,"obj":"connection","method":"auth:cookie_begin","params":{"client_nonce":"{{clientNonce}}"} }""";

			TorControlReply authChallengeReply = await controlClient.SendCommandAsync(json, cancellationToken).ConfigureAwait(false);

			if (!authChallengeReply)
			{
				Logger.LogError($"Received invalid reply for our auth:cookie_begin: '{authChallengeReply}'");
				throw new TorControlException("Invalid status code in auth:cookie_begin reply.");
			}

			var authChallengeResponse = JsonSerializer.Deserialize<JsonRpcResponse<AuthChallengeResult>>(authChallengeReply.ResponseLines[0]);

			if (authChallengeResponse is null || !authChallengeResponse.Deconstruct(out var authChallengeResult, out _))
			{
				throw new TorControlException("Unexpected reply to auth:cookie_begin.");
			}

			var tupleHash = new TupleHash(bitLength: 128, S: TupleHashCustomization);
			tupleHash.BlockUpdate(Convert.FromHexString(cookieString));
			tupleHash.BlockUpdate("Server"u8);
			tupleHash.BlockUpdate(Encoding.ASCII.GetBytes("inet:127.0.0.1:9180"));
			tupleHash.BlockUpdate(Convert.FromHexString(clientNonce));
			tupleHash.BlockUpdate(Convert.FromHexString(authChallengeResult.ServerNonce));

			int digestSize = tupleHash.GetDigestSize();
			byte[] serverHash = new byte[digestSize];
			tupleHash.DoFinal(serverHash, 0);

			string serverHashStr = Convert.ToHexString(serverHash);

			if (authChallengeResult.ServerMac != serverHashStr)
			{
				Logger.LogError($"Server MAC is different than ours: '{authChallengeResult.ServerMac} != {serverHashStr}'");
				throw new TorControlException("Different server MAC.");
			}

			tupleHash = new TupleHash(bitLength: 128, S: TupleHashCustomization);
			tupleHash.BlockUpdate(Convert.FromHexString(cookieString));
			tupleHash.BlockUpdate("Client"u8);
			tupleHash.BlockUpdate(Encoding.ASCII.GetBytes("inet:127.0.0.1:9180"));
			tupleHash.BlockUpdate(Convert.FromHexString(clientNonce));
			tupleHash.BlockUpdate(Convert.FromHexString(authChallengeResult.ServerNonce));

			byte[] clientHash = new byte[tupleHash.GetDigestSize()];
			tupleHash.DoFinal(clientHash, 0);

			string clientHashStr = Convert.ToHexString(clientHash);

			Logger.LogTrace($"Authenticate using server hash: '{clientHashStr}'.");
			json = $$"""{"id": 2,"obj":"{{authChallengeResult.CookieAuth}}","method":"auth:cookie_continue","params":{"client_mac":"{{clientHashStr}}"} }""";

			authenticationReply = await controlClient.SendCommandAsync(json, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// Authentication was proved by accessing the RPC by its UNIX socket domain file.
			var json = """{"id": 1,"obj":"connection","method":"auth:authenticate","params":{"scheme":"auth:inherent"} }""";
			authenticationReply = await controlClient.SendCommandAsync(json, cancellationToken).ConfigureAwait(false);
		}

		if (!authenticationReply)
		{
			Logger.LogError($"Invalid reply: '{authenticationReply}'");
			throw new TorControlException("Invalid status in AUTHENTICATE reply.");
		}

		if (authenticationReply.ResponseLines.Count > 0)
		{
			Logger.LogDebug($"Arti authentication response: {authenticationReply.ResponseLines[0]}.");
		}

		return controlClient;
	}

	[GeneratedRegex("^AUTHCHALLENGE SERVERHASH=([a-fA-F0-9]+) SERVERNONCE=([a-fA-F0-9]+)$")]
	private static partial Regex AuthChallengeRegex();
}
