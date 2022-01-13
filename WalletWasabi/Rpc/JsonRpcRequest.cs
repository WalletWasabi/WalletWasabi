using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Rpc;

/// <summary>
/// A rpc call is represented by sending a Request object to a Server. The Request object has the following members:
/// + jsonrpc - A String specifying the version of the JSON-RPC protocol. MUST be exactly "2.0".
/// + method  - A String containing the name of the method to be invoked. Method names that begin with the word rpc
///             followed by a period character (U+002E or ASCII 46) are reserved for rpc-internal methods and extensions
///             and MUST NOT be used for anything else.
/// + params  - A Structured value that holds the parameter values to be used during the invocation of the method. This
///             member MAY be omitted.
/// + id      - An identifier established by the Client that MUST contain a String, Number, or NULL value if included.
///             If it is not included it is assumed to be a notification. The value SHOULD normally not be Null [1] and
///             Numbers SHOULD NOT contain fractional parts [2].
/// </summary>
public class JsonRpcRequest
{
	/// <summary>
	/// Constructor used to deserialize the requests.
	/// </summary>
	[JsonConstructor]
	public JsonRpcRequest(string jsonrpc, string id, string method, JToken parameters)
	{
		JsonRPC = jsonrpc;
		Id = id;
		Method = method;
		Parameters = parameters;
	}

	/// <summary>
	/// Gets a value indicating whether the JsonRpcRequest is a notification request
	/// which means the client is not interested in receiving a response.
	/// </summary>
	[MemberNotNullWhen(returnValue: false, nameof(Id))]
	public bool IsNotification => Id is null;

	/// <summary>
	/// Gets the version of the JSON-RPC protocol. MUST be exactly "2.0".
	/// </summary>
	[JsonProperty("jsonrpc", Required = Required.Default)]
	public string JsonRPC { get; }

	/// <summary>
	/// Gets the identifier established by the Client that MUST contain a String,
	/// Number, or NULL value if included. If it is not included it is assumed
	/// to be a notification.
	/// The value SHOULD normally not be Null and Numbers SHOULD NOT contain
	/// fractional parts.
	/// The use of Null as a value for the id member in a Request object is
	/// discouraged, because this specification uses a value of Null for Responses
	/// with an unknown id. Also, because JSON-RPC 1.0 uses an id value of Null
	/// for Notifications this could cause confusion in handling.
	/// The Server MUST reply with the same value in the Response object if included.
	/// This member is used to correlate the context between the two objects.
	/// </summary>
	[JsonProperty("id")]
	public string Id { get; }

	/// <summary>
	/// Gets the name of the method to be invoked. Method names that
	/// begin with the word rpc followed by a period character (U+002E or ASCII 46)
	/// are reserved for rpc-internal methods and extensions and MUST NOT be used
	/// for anything else.
	/// </summary>
	[JsonProperty("method", Required = Required.Always)]
	public string Method { get; }

	/// <summary>
	/// Gets a structured value that holds the parameter values to be used during the
	/// invocation of the method. This member MAY be omitted.
	/// </summary>
	[JsonProperty("params")]
	public JToken Parameters { get; }

	/// <summary>
	/// Parses the json rpc request giving back the deserialized JsonRpcRequest instance.
	/// Return true if the deserialization was successful, otherwise false.
	/// </summary>
	public static bool TryParse(string rawJson, [NotNullWhen(true)] out JsonRpcRequest[]? requests, out bool isBatch)
	{
		try
		{
			isBatch = rawJson.TrimStart().StartsWith("[");
			rawJson = isBatch ? rawJson : $"[{rawJson}]";
			requests = JsonConvert.DeserializeObject<JsonRpcRequest[]>(rawJson);
			return true;
		}
		catch (JsonException)
		{
			requests = null;
			isBatch = false;
			return false;
		}
	}
}
