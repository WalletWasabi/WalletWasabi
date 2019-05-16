using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.Gui.Rpc
{
	/// <summary>
	/// A rpc call is represented by sending a Request object to a Server. The Request object has the following members:
	/// </summary>
	public class JsonRpcRequest
	{
		/// <summary>
		/// Parses the json rpc request giving back the deserialized JsonRpcRequest instance.
		/// Return true if the deserialization was successful, otherwise false.
		/// </summary>
		public static bool TryParse(string rawJson, out JsonRpcRequest request)
		{
			try
			{
				request = JsonConvert.DeserializeObject<JsonRpcRequest>(rawJson);
				return true;
			}
			catch(JsonException)
			{
				request = null;
				return false;
			}
		}

		/// <summary>
		/// Constructor used to deserialize the requests
		/// </summary>
		[JsonConstructor]
		public  JsonRpcRequest(string jsonrpc, string id, string method, JToken parameters)
		{
			JsonRPC = jsonrpc;
			Id = id;
			Method = method;
			Parameters = parameters;
		}

		/// <summary>
		/// Requests with null Id are called notification requests and indicate the
		/// client is not interested in receiving a response.
		/// </summary>
		public bool IsNotification => Id == null;

		/// <summary>
		/// A String specifying the version of the JSON-RPC protocol. MUST be exactly "2.0".
		/// </summary>
		[JsonProperty("jsonrpc", Required = Required.Always)]
		public string JsonRPC { get; }

		/// <summary>
		/// An identifier established by the Client that MUST contain a String, 
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
		/// A String containing the name of the method to be invoked. Method names that
		/// begin with the word rpc followed by a period character (U+002E or ASCII 46)
		/// are reserved for rpc-internal methods and extensions and MUST NOT be used 
		/// for anything else.
		/// </summary>
		[JsonProperty("method", Required = Required.Always)]
		public string Method { get; }

		/// <summary>
		/// A Structured value that holds the parameter values to be used during the
		/// invocation of the method. This member MAY be omitted.
		/// </summary>
		[JsonProperty("params")]
		public JToken Parameters { get;  }
	}
}
