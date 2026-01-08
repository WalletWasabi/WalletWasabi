using NBitcoin;
using Org.BouncyCastle.Crypto.Digests;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Rpc;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control;

/// <summary>
/// Class to authenticate to Tor Control.
/// </summary>
public partial class TorControlClientFactory
{
	/// <summary>Client HMAC-SHA256 key for AUTHCHALLENGE.</summary>
	/// <remarks>Server's HMAC key is <c>Tor safe cookie authentication server-to-controller hash</c></remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">Section 3.24. AUTHCHALLENGE</seealso>
	private static byte[] ClientHmacKey = Encoding.ASCII.GetBytes("Tor safe cookie authentication controller-to-server hash");

	/// <summary>Customization bytes used by Arti when hashing using TupleHash.</summary>
	/// <seealso href="https://gitlab.torproject.org/tpo/core/arti/-/blob/main/doc/dev/rpc-book/src/rpc-cookie-spec.md#preliminaries"/>
	private static byte[] ArtiTupleHashCustomization = Encoding.ASCII.GetBytes("arti-rpc-cookie-v1");

	public TorControlClientFactory(IRandom? random = null)
	{
		_random = random ?? new UnsecureRandom();
	}

	/// <summary>Helps generate nonces for AUTH challenges.</summary>
	private readonly IRandom _random;

	/// <summary>Connects to Tor Control endpoint and authenticates using safe-cookie mechanism.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section 3.5</seealso>
	/// <exception cref="TorControlException">If TCP connection cannot be established OR if authentication fails for some reason.</exception>
	public async Task<TorControlClient> ConnectAndAuthenticateAsync(TorBackend backend, EndPoint endPoint, string? cookieString, CancellationToken cancellationToken)
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
			TorControlClient controlClient = clientToDispose = new(backend, socket);

			if (backend == TorBackend.CTor)
			{
				if (cookieString is null)
				{
					throw new TorControlException("CTor implementation requires a cookie string for SAFECOOKIE authentication.");
				}

				await AuthSafeCookieOrThrowAsync(controlClient, cookieString, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				var rpcObjectId = await ArtiAuthOrThrowAsync(controlClient, cookieString, cancellationToken).ConfigureAwait(false);
				controlClient.RpcSessionId = rpcObjectId;
			}

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

	/// <summary>Authenticates Arti client using SAFE-COOKIE or UNIX socket domain.</summary>
	/// <exception cref="TorControlException">If authentication fails for some reason.</exception>
	internal async Task<string> ArtiAuthOrThrowAsync(TorControlClient controlClient, string? cookieString, CancellationToken cancellationToken)
	{
		byte[] nonceBytes = new byte[32];
		_random.GetBytes(nonceBytes);
		string clientNonce = Convert.ToHexString(nonceBytes);

		string rpcSessionId;

		// Cookie authentication.
		if (cookieString is not null)
		{
			var authRequest = controlClient.CreateCookieAuthBeginRpcRequest(clientNonce);
			var authChallengeReply = await controlClient.SendRpcRequestAsync<CookieAuthChallengeResult>(authRequest, cancellationToken).ConfigureAwait(false);

			if (!authChallengeReply.Deconstruct(out var authChallengeResult, out var _))
			{
				Logger.LogError($"Received invalid reply for our auth:cookie_begin: '{authChallengeReply}'");
				throw new TorControlException("Invalid status code in auth:cookie_begin reply.");
			}

			var serverHash = ComputeTupleHash(cookieString, "Server", clientNonce, authChallengeResult.ServerNonce);
			var serverHashStr = Convert.ToHexString(serverHash);

			if (authChallengeResult.ServerMac != serverHashStr)
			{
				Logger.LogError($"Server MAC is different than ours: '{authChallengeResult.ServerMac} != {serverHashStr}'");
				throw new TorControlException("Different server MAC.");
			}

			var clientHash = ComputeTupleHash(cookieString, "Client", clientNonce, authChallengeResult.ServerNonce);
			var clientHashStr = Convert.ToHexString(clientHash);

			Logger.LogTrace($"Authenticate using server hash: '{clientHashStr}'.");
			var continueRequest = controlClient.CreateCookieAuthContinueRpcRequest(authChallengeResult.CookieAuth, clientHashStr);
			var authenticationResponse = await controlClient.SendRpcRequestAsync<AuthSessionResult>(continueRequest, cancellationToken).ConfigureAwait(false);

			if (!authenticationResponse.Deconstruct(out var result, out var error))
			{
				Logger.LogError($"Error: '{error}'");
				throw new TorControlException("Invalid status in AUTHENTICATE reply.");
			}

			rpcSessionId = result.Session;
		}
		else
		{
			// Authentication was proved by accessing the RPC by its UNIX socket domain file.
			var authRequest = controlClient.CreateInherentAuthRpcRequest();
			var authenticationResponse = await controlClient.SendRpcRequestAsync<AuthSessionResult>(authRequest, cancellationToken).ConfigureAwait(false);

			if (!authenticationResponse.Deconstruct(out var result, out var error))
			{
				Logger.LogError($"Error: '{error}'");
				throw new TorControlException("Invalid status in AUTHENTICATE reply.");
			}

			rpcSessionId = result.Session;
		}

		string rpcClientId;

		// Get a client ID.
		{
			var request = controlClient.CreateGetClientRpcRequest(rpcSessionId);
			var clientResponse = await controlClient.SendRpcRequestAsync<GetClientResult>(request, cancellationToken).ConfigureAwait(false);

			if (!clientResponse.Deconstruct(out var result, out var error))
			{
				Logger.LogError($"Error: '{error}'");
				throw new TorControlException("Invalid status in AUTHENTICATE reply.");
			}

			rpcClientId = result.Id;
		}

		controlClient.RpcSessionId = rpcSessionId;
		controlClient.RpcClientId = rpcClientId;

		Logger.LogDebug($"Session ID: {rpcSessionId}.");
		Logger.LogDebug($"Client ID: {rpcClientId}.");
		return rpcSessionId;
	}

	/// <summary>Authenticates C Tor client using SAFE-COOKIE.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section 3.24 for SAFECOOKIE authentication.</seealso>
	/// <seealso href="https://github.com/torproject/stem/blob/63a476056017dda5ede35efc4e4f7acfcc1d7d1a/stem/connection.py#L893">Python implementation.</seealso>
	/// <exception cref="TorControlException">If authentication fails for some reason.</exception>
	internal async Task<TorControlClient> AuthSafeCookieOrThrowAsync(TorControlClient controlClient, string cookieString, CancellationToken cancellationToken)
	{
		byte[] nonceBytes = new byte[32];
		_random.GetBytes(nonceBytes);
		string clientNonce = Convert.ToHexString(nonceBytes);

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
		Match match = AuthChallengeRegex().Match(reply);

		if (!match.Success)
		{
			Logger.LogError($"Invalid reply: '{reply}'");
			throw new TorControlException("AUTHCHALLENGE reply cannot be parsed.");
		}

		string serverNonce = match.Groups[2].Value;
		string toHash = $"{cookieString}{clientNonce}{serverNonce}";

		using HMACSHA256 hmacSha256 = new(ClientHmacKey);
		byte[] serverHash = hmacSha256.ComputeHash(Convert.FromHexString(toHash));
		string serverHashStr = Convert.ToHexString(serverHash);

		Logger.LogTrace($"Authenticate using server hash: '{serverHashStr}'.");
		TorControlReply authenticationReply = await controlClient.SendCommandAsync($"AUTHENTICATE {serverHashStr}\r\n", cancellationToken).ConfigureAwait(false);

		if (!authenticationReply)
		{
			Logger.LogError($"Invalid reply: '{authenticationReply}'");
			throw new TorControlException("Invalid status in AUTHENTICATE reply.");
		}

		return controlClient;
	}

	/// <summary>Computes the tuple hash used in Arti's RPC cookie authentication.</summary>
	private static byte[] ComputeTupleHash(string cookieString, string side, string clientNonce, string serverNonce)
	{
		var tupleHash = new TupleHash(bitLength: 128, S: ArtiTupleHashCustomization);
		tupleHash.BlockUpdate(Convert.FromHexString(cookieString));
		tupleHash.BlockUpdate(Encoding.ASCII.GetBytes(side));
		tupleHash.BlockUpdate(Encoding.ASCII.GetBytes("inet:127.0.0.1:9180"));
		tupleHash.BlockUpdate(Convert.FromHexString(clientNonce));
		tupleHash.BlockUpdate(Convert.FromHexString(serverNonce));

		var digestSize = tupleHash.GetDigestSize();
		var hash = new byte[digestSize];
		tupleHash.DoFinal(hash, 0);

		return hash;
	}

	[GeneratedRegex("^AUTHCHALLENGE SERVERHASH=([a-fA-F0-9]+) SERVERNONCE=([a-fA-F0-9]+)$")]
	private static partial Regex AuthChallengeRegex();
}
