using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Rpc;

public abstract record JsonRpcResponse
{
	// Default error messages for standard JsonRpcErrorCodes
	private static Dictionary<JsonRpcErrorCodes, string> Messages = new()
	{
		[JsonRpcErrorCodes.ParseError] = "Parse error",
		[JsonRpcErrorCodes.InvalidRequest] = "Invalid Request",
		[JsonRpcErrorCodes.MethodNotFound] = "Method not found",
		[JsonRpcErrorCodes.InvalidParams] = "Invalid params",
		[JsonRpcErrorCodes.InternalError] = "Internal error",
	};

	protected JsonRpcResponse(string id)
	{
		Id = id;
	}

	[JsonProperty("jsonrpc", Order = 0)]
	public string JsonRpc => "2.0";

	[JsonProperty("id", Order = 3)]
	public string Id { get; }

	public static JsonRpcSuccessResponse CreateResultResponse(string id, object? result = null) =>
		new(id, result);

	public static JsonRpcErrorResponse CreateErrorResponse(string id, JsonRpcErrorCodes code, string? customMessage = null) =>
		new(id, code, customMessage ?? GetDefaultMessageFor(code));

	public string ToJson(JsonSerializerSettings serializerSettings) =>
		JsonConvert.SerializeObject(this, serializerSettings);

	private static string GetDefaultMessageFor(JsonRpcErrorCodes code) =>
		Messages.TryGetValue(code, out var rpcErrorMessage)
			? rpcErrorMessage
			: "Server error";
}

public record JsonRpcSuccessResponse : JsonRpcResponse
{
	public JsonRpcSuccessResponse(string id, object? result)
		: base(id)
	{
		Result = result;
	}

	[JsonProperty("result", Order = 1)]
	public object? Result { get; }
}

public record JsonRpcErrorResponse : JsonRpcResponse
{
	public JsonRpcErrorResponse(string id, JsonRpcErrorCodes code, string message)
		: base(id)
	{
		Error = new ErrorObject(code, message);
	}

	[JsonProperty("error", Order = 1)]
	public ErrorObject Error { get; }

	public record ErrorObject(JsonRpcErrorCodes code, string message);
}
