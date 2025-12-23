using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace WalletWasabi.Tor.Control.Json;

public record JsonRpcError(
	[property: JsonPropertyName("code")] int Code,
	[property: JsonPropertyName("message")] string Message,
	[property: JsonPropertyName("data")] object? Data = null
);

public record JsonRpcResponse<T>(
	[property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0",
	[property: JsonPropertyName("result")] T? Result = default,
	[property: JsonPropertyName("error")] JsonRpcError? Error = null,
	[property: JsonPropertyName("id")] object? Id = null)
{
	public bool Deconstruct([NotNullWhen(true)] out T? result, [NotNullWhen(false)] out JsonRpcError? error)
	{
		result = Result;
		error = Error;

		return Error is null;
	}
}

public sealed record AuthChallengeResult(
    [property: JsonPropertyName("cookie_auth")]
    string CookieAuth,

    [property: JsonPropertyName("server_addr")]
    string ServerAddr,

    [property: JsonPropertyName("server_mac")]
    string ServerMac,

    [property: JsonPropertyName("server_nonce")]
    string ServerNonce
);
