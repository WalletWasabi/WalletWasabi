using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Rpc;

public record JsonRpcResponse
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

	[JsonProperty("jsonrpc", Order = 0)]
	public string JsonRpc => "2.0";

	[JsonProperty("id", Order = 3)]
	public string Id { get; }

	protected JsonRpcResponse(string id)
	{
		Id = id;
	}

	public static JsonRpcSuccessResponse CreateResultResponse(string id, object result)
	{
		return new JsonRpcSuccessResponse(id, result);
	}

	public static JsonRpcErrorResponse CreateErrorResponse(string id, JsonRpcErrorCodes code, string? customMessage = null)
	{
		var defaultMessage = Messages.TryGetValue(code, out var rpcErrorMessage)
			? rpcErrorMessage
			: "Server error";

		return new JsonRpcErrorResponse(id, code, customMessage ?? defaultMessage);
	}

	public string ToJson(JsonSerializerSettings serializerSettings)
	{
		return JsonConvert.SerializeObject(this, serializerSettings);
	}
}

public record JsonRpcSuccessResponse : JsonRpcResponse
{
	public JsonRpcSuccessResponse(string id, object result)
		: base(id)
	{
		Result = result;
	}

	[JsonProperty("result", Order = 1)]
	public object Result { get; }
}

public record JsonRpcErrorResponse : JsonRpcResponse
{
	public record ErrorObject(JsonRpcErrorCodes code, string message);

	public JsonRpcErrorResponse(string id, JsonRpcErrorCodes code, string message)
		: base(id)
	{
		Error = new ErrorObject(code, message);
	}

	[JsonProperty("error", Order = 1)]
	public ErrorObject Error { get; }
}
