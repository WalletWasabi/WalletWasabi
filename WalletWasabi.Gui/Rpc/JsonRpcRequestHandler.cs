using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Gui.Rpc
{
	///<summary>
	/// This class coordinates all the major steps in processing the RPC call.
	/// It parses the json request, parses the parameters, invokes the service
	/// methods and handles the errors.
	///</summary>
	public class JsonRpcRequestHandler<TService>
	{
		private static readonly JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Converters = new JsonConverter[] {
				new Uint256JsonConverter(),
				new OutPointJsonConverter(),
				new BitcoinAddressJsonConverter()
			}
		};

		private static readonly JsonSerializer DefaultSerializer = JsonSerializer.Create(DefaultSettings);

		private TService Service { get; }
		private JsonRpcServiceMetadataProvider MetadataProvider { get; }

		public JsonRpcRequestHandler(TService service)
		{
			Service = service;
			MetadataProvider = new JsonRpcServiceMetadataProvider(typeof(TService));
		}

		/// <summary>
		/// Parses the request and dispatches it to the correct service's method.
		/// </summary>
		/// <param name="body">The raw rpc request.</param>
		/// <param name="cancellationToken">The cancellation token that will be past to the service handler in case it expects/accepts one.</param>
		/// <returns>The response that, after serialization, is returned as response.</returns>
		public async Task<string> HandleAsync(string body, CancellationToken cancellationToken)
		{
			if (!JsonRpcRequest.TryParse(body, out var jsonRpcRequest))
			{
				return JsonRpcResponse.CreateErrorResponse(null, JsonRpcErrorCodes.ParseError).ToJson(DefaultSettings);
			}
			var methodName = jsonRpcRequest.Method;

			if (!MetadataProvider.TryGetMetadata(methodName, out var prodecureMetadata))
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
						parameters.Add(jarr[i].ToObject(methodParameters[i].type, DefaultSerializer));
					}
				}
				else if (jsonRpcRequest.Parameters is JObject jobj)
				{
					for (int i = 0; i < methodParameters.Count; i++)
					{
						var param = methodParameters[i];
						if (!jobj.ContainsKey(param.name))
						{
							return Error(JsonRpcErrorCodes.InvalidParams,
								$"A value for the '{param.name}' is missing.", jsonRpcRequest.Id);
						}
						parameters.Add(jobj[param.name].ToObject(param.type, DefaultSerializer));
					}
				}

				// Special case: if there is a missing parameter and the procedure is expecting a CancellationTokenSource
				// then pass the cancellationToken we have. This will allow us to cancel async requests when the server is stopped.
				if (parameters.Count == methodParameters.Count - 1)
				{
					var position = methodParameters.FindIndex(x => x.type == typeof(CancellationToken));
					if (position > -1)
					{
						parameters.Insert(position, cancellationToken);
					}
				}
				if (parameters.Count != methodParameters.Count)
				{
					return Error(JsonRpcErrorCodes.InvalidParams,
						$"{methodParameters.Count} parameters were expected but {parameters.Count} were received.", jsonRpcRequest.Id);
				}
				var result = prodecureMetadata.MethodInfo.Invoke(Service, parameters.ToArray());

				if (jsonRpcRequest.IsNotification) // the client is not interested in getting a response
				{
					return string.Empty;
				}

				JsonRpcResponse response = null;
				if (prodecureMetadata.MethodInfo.IsAsync())
				{
					if (!prodecureMetadata.MethodInfo.ReturnType.IsGenericType)
					{
						await ((Task)result).ConfigureAwait(false);
						response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, null);
					}
					else
					{
						var ret = await ((dynamic)result).ConfigureAwait(false);
						response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, ret);
					}
				}
				else
				{
					response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, result);
				}
				return response.ToJson(DefaultSettings);
			}
			catch (TargetInvocationException e)
			{
				return Error(JsonRpcErrorCodes.InternalError, e.InnerException.Message, jsonRpcRequest.Id);
			}
			catch (Exception e)
			{
				return Error(JsonRpcErrorCodes.InternalError, e.Message, jsonRpcRequest.Id);
			}
		}

		private string Error(JsonRpcErrorCodes code, string reason, string id)
		{
			var response = JsonRpcResponse.CreateErrorResponse(id, code, reason);
			return id is { }
				? response.ToJson(DefaultSettings)
				: string.Empty;
		}
	}
}
