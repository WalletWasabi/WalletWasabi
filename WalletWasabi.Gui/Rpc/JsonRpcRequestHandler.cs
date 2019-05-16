using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Gui.Rpc
{
	///<summary>
	/// This class coordinates all the major steps in processing the RPC call.
	/// It parses the json request, parses the parameters, invoke the service
	/// methods and handles the errors.
	///</summary>
	public class JsonRpcRequestHandler
	{
		private static JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Converters = new JsonConverter[] { 
				new Uint256JsonConverter(), 
				new OutPointJsonConverter(),
				new BitcoinAddressJsonConverter() 
			}
		};

		private static JsonSerializer DefaultSerializer = JsonSerializer.Create(DefaultSettings);

		private readonly object _service;
		private readonly JsonRpcServiceMetadataProvider _metadataProvider;

		public JsonRpcRequestHandler(object service)
		{
			_service = service;
			_metadataProvider = new JsonRpcServiceMetadataProvider(_service);
		}

		public async Task<string> HandleAsync(string body, CancellationTokenSource cts)
		{
			if(!JsonRpcRequest.TryParse(body, out var jsonRpcRequest))
			{
				return JsonRpcResponse.CreateErrorResponse(null, JsonRpcErrorCodes.ParseError).ToJson(DefaultSettings);
			}
			var methodName = jsonRpcRequest.Method;

			if(!_metadataProvider.TryGetMetadata(methodName, out var prodecureMetadata))
			{
				return Error(JsonRpcErrorCodes.MethodNotFound, $"'{methodName}' method not found.", jsonRpcRequest.Id);
			}

			try
			{
				var methodParameters = prodecureMetadata.Parameters;
				var parameters = new List<object>();

				if (jsonRpcRequest.Parameters is JArray jarr)
				{
					var count = methodParameters.Count < jarr.Count ? methodParameters.Count : jarr.Count;  
					for (int i = 0; i < count; i++)
					{
						parameters.Add( jarr[i].ToObject(methodParameters[i].type, DefaultSerializer) );
					}
				}
				else if (jsonRpcRequest.Parameters is JObject jobj)
				{
					for (int i = 0; i < methodParameters.Count; i++)
					{
						var param = methodParameters[i];
						if(!jobj.ContainsKey(param.name))
						{
							return Error(JsonRpcErrorCodes.InvalidParams, 
								$"A value for the '{param.name}' is missing.", jsonRpcRequest.Id);
						}
						parameters.Add( jobj[param.name].ToObject(param.type, DefaultSerializer));
					}
				}

				// Special case: if there is a missing parameter and the procedure is expecting a CancellationTokenSource
				// then pass the cts we have. This will allow us to cancel async requests when the server is stopped. 
				if (parameters.Count == methodParameters.Count -1)
				{
					var position = methodParameters.FindIndex(x=>x.type == typeof(CancellationTokenSource));
					if(position > -1)
					{
						parameters.Insert(position, cts);
					}
				}
				if (parameters.Count != methodParameters.Count)
				{
					return Error(JsonRpcErrorCodes.InvalidParams, 
						$"{methodParameters.Count} parameters were expected but {parameters.Count} were received.", jsonRpcRequest.Id);
				}
				var result =  prodecureMetadata.MethodInfo.Invoke(_service, parameters.ToArray());

				if (jsonRpcRequest.IsNotification) // the client is not interested in getting a response
				{
					return string.Empty;
				}

				JsonRpcResponse response = null;
				if(prodecureMetadata.MethodInfo.IsAsync())
				{
					if(!prodecureMetadata.MethodInfo.ReturnType.IsGenericType)
					{
						await (Task)result;
						response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, null);
					}
					else
					{
						var ret = await (dynamic) result;
						response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, ret);
					}
				}
				else
				{
					response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, result);
				}
				return response.ToJson(DefaultSettings);
			}
			catch(TargetInvocationException e)
			{
				return Error(JsonRpcErrorCodes.InternalError, e.InnerException.Message, jsonRpcRequest.Id);
			}
			catch(Exception e)
			{
				return Error(JsonRpcErrorCodes.InternalError, e.Message, jsonRpcRequest.Id);
			}
		}

		private string Error(JsonRpcErrorCodes code, string reason, string id)
		{
			var response = JsonRpcResponse.CreateErrorResponse(id, code, reason);
			return id == null 
				? string.Empty 
				: response.ToJson(DefaultSettings);
		}
	}
}