using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Gui.Rpc
{
    public class JsonRpcResponse
    {
        // Default error messages for standard JsonRpcErrorCodes
        private static Dictionary<JsonRpcErrorCodes, string> Messages = new Dictionary<JsonRpcErrorCodes, string>
        {
            [JsonRpcErrorCodes.ParseError] = "Parse error",
            [JsonRpcErrorCodes.InvalidRequest] = "Invalid Request",
            [JsonRpcErrorCodes.MethodNotFound] = "Method not found",
            [JsonRpcErrorCodes.InvalidParams] = "Invalid params",
            [JsonRpcErrorCodes.InternalError] = "Internal error",
        };

        [JsonProperty("jsonrpc", Order = 0)]
        public string JsonRpc => "2.0";

        [JsonProperty("result", Order = 1)]
        public object Result { get; }

        [JsonProperty("error", Order = 1)]
        public object Error { get; }

        [JsonProperty("id", Order = 3)]
        public string Id { get; }

        public static JsonRpcResponse CreateResultResponse(string id, object result)
        {
            return new JsonRpcResponse(id, result, null);
        }

        public static JsonRpcResponse CreateErrorResponse(string id, JsonRpcErrorCodes code, string message = null)
        {
            var error = new
            {
                code,
                message = message ?? GetMessage(code)
            };
            return new JsonRpcResponse(id, null, error);
        }

        private JsonRpcResponse(string id, object result, object error)
        {
            Id = id;
            Result = result;
            Error = error;
        }

        private static string GetMessage(JsonRpcErrorCodes code)
        {
            if (Messages.TryGetValue(code, out var message))
            {
                return message;
            }
            return "Server error";
        }

        public string ToJson(JsonSerializerSettings serializerSettings)
        {
            return JsonConvert.SerializeObject(this, serializerSettings);
        }
    }
}
