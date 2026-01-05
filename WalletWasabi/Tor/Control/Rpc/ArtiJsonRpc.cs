using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control.Rpc;

public record JsonRpcRequest(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("obj")] string Obj,
	[property: JsonPropertyName("method")] string Method,
	[property: JsonPropertyName("params")] Dictionary<string, object> Params
);

public record JsonRpcError(
	[property: JsonPropertyName("code")] int Code,
	[property: JsonPropertyName("message")] string Message,
	[property: JsonPropertyName("data")] object? Data = null
);

public record JsonRpcResponse<T>(
	[property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0",
	[property: JsonPropertyName("result")] T? Result = default,
	[property: JsonPropertyName("error")] JsonRpcError? Error = null,
	[property: JsonPropertyName("id")] object? Id = null) : ITorControlReply
{
	public bool Deconstruct([NotNullWhen(true)] out T? result, [NotNullWhen(false)] out JsonRpcError? error)
	{
		result = Result;
		error = Error;

		return Error is null;
	}
}

public sealed record CookieAuthChallengeResult(
    [property: JsonPropertyName("cookie_auth")]
    string CookieAuth,

    [property: JsonPropertyName("server_addr")]
    string ServerAddress,

    [property: JsonPropertyName("server_mac")]
    string ServerMac,

    [property: JsonPropertyName("server_nonce")]
    string ServerNonce
);

public record AuthSessionResult(
    [property: JsonPropertyName("session")]
	string Session
);

public record GetClientResult(
    [property: JsonPropertyName("id")]
	string Id
);


public record GetClientStatusResult(
	[property: JsonPropertyName("ready")]
	bool Ready,
	[property: JsonPropertyName("fraction")]
	double Fraction,
	[property: JsonPropertyName("blocked")]
	object? Blocked
);
