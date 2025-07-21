using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.JsonConverters;
using WalletWasabi.Rpc.JsonConverters;

namespace WalletWasabi.Rpc;

/// <summary>
/// This class coordinates all the major steps in processing the RPC call.
/// It parses the json request, parses the parameters, invokes the service
/// methods and handles the errors.
/// </summary>
public class JsonRpcRequestHandler<TService>
	where TService : notnull
{
	private readonly JsonSerializerSettings _defaultSettings;

	private readonly JsonSerializer _defaultSerializer;

	public JsonRpcRequestHandler(TService service, Network network)
	{
		_service = service;
		_metadataProvider = new JsonRpcServiceMetadataProvider(service.GetType());
		_defaultSettings = new()
    	{
    		NullValueHandling = NullValueHandling.Ignore,
    		ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

    		Converters = new JsonConverter[]
    		{
    			new Uint256JsonConverter(),
    			new OutPointAsTxoRefJsonConverter(),
    			new BitcoinAddressJsonConverter(),
    			new DestinationJsonConverter(network)
    		}
    	};
		_defaultSerializer = JsonSerializer.Create(_defaultSettings);
	}

	private readonly TService _service;
	private readonly JsonRpcServiceMetadataProvider _metadataProvider;

	/// <summary>
	/// Parses the request and dispatches it to the correct service's method.
	/// </summary>
	/// <param name="body">The raw RPC request.</param>
	/// <param name="cancellationToken">The cancellation token that will be past to the service handler in case it expects/accepts one.</param>
	/// <returns>The response that, after serialization, is returned as response.</returns>
	public async Task<string> HandleAsync(string path, string body, CancellationToken cancellationToken)
	{
		if (!JsonRpcRequest.TryParse(body, out var jsonRpcRequests, out var isBatch))
		{
			return CreateParseErrorResponse();
		}

		return await HandleRequestsAsync(path, jsonRpcRequests, isBatch, cancellationToken).ConfigureAwait(false);
	}

	public string CreateParseErrorResponse()
	{
		return JsonRpcResponse.CreateErrorResponse(null, JsonRpcErrorCodes.ParseError).ToJson(_defaultSettings);
	}

	public async Task<string> HandleRequestsAsync(string path, JsonRpcRequest[] jsonRpcRequests, bool isBatch, CancellationToken cancellationToken)
	{
		var results = new List<string>();

		foreach (var jsonRpcRequest in jsonRpcRequests)
		{
			cancellationToken.ThrowIfCancellationRequested();

			string jsonResult = await HandleRequestAsync(path, jsonRpcRequest, cancellationToken).ConfigureAwait(false);
			results.Add(jsonResult);
		}

		return isBatch ? $"[{string.Join(",", results)}]" : results[0];
	}

	private async Task<string> HandleRequestAsync(string path, JsonRpcRequest jsonRpcRequest, CancellationToken cancellationToken)
	{
		var methodName = jsonRpcRequest.Method;

		if (!_metadataProvider.TryGetMetadata(methodName, out var procedureMetadata))
		{
			return Error(JsonRpcErrorCodes.MethodNotFound, $"'{methodName}' method not found.", jsonRpcRequest.Id);
		}

		try
		{
			var methodParameters = procedureMetadata.Parameters;
			var parameters = new List<object>();

			if (jsonRpcRequest.Parameters is JArray jArray)
			{
				var count = methodParameters.Count < jArray.Count ? methodParameters.Count : jArray.Count;
				for (int i = 0; i < count; i++)
				{
					var parameter = methodParameters[i];
					var item = jArray[i].ToObject(parameter.type, _defaultSerializer)
						?? throw new InvalidOperationException($"Parameter `{parameter.name}` is null.");
					parameters.Add(item);
				}
			}
			else if (jsonRpcRequest.Parameters is JObject jObj)
			{
				for (int i = 0; i < methodParameters.Count; i++)
				{
					var parameter = methodParameters[i];
					if (!jObj.ContainsKey(parameter.name))
					{
						if (parameter.isOptional)
						{
							parameters.Add(parameter.defaultValue);
							continue;
						}
						return Error(
							JsonRpcErrorCodes.InvalidParams,
							$"A value for the '{parameter.name}' is missing.",
							jsonRpcRequest.Id);
					}

					var parameterValue = jObj[parameter.name]!;
					if (parameterValue.ToObject(parameter.type, _defaultSerializer) is not { } parameterTypedValue)
					{
						return Error(
							JsonRpcErrorCodes.InvalidParams,
							$"A value for the '{parameter.name}' is not of the expected type.",
							jsonRpcRequest.Id);
					}
					parameters.Add(parameterTypedValue);
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
			if (parameters.Count < methodParameters.Count(x => !x.isOptional))
			{
				return Error(
					JsonRpcErrorCodes.InvalidParams,
					$"{methodParameters.Count} parameters were expected but {parameters.Count} were received.",
					jsonRpcRequest.Id);
			}

			var missingParameters = methodParameters.Count - parameters.Count;
			parameters.AddRange(methodParameters.TakeLast(missingParameters).Select(x => x.defaultValue));

			if (procedureMetadata.RequiresInitialization && _metadataProvider.TryGetInitializer(out var initializer))
			{
				initializer.Invoke(_service, new object[] { path, procedureMetadata.RequiresInitialization });
			}

			var result = procedureMetadata.MethodInfo.Invoke(_service, parameters.ToArray());

			if (jsonRpcRequest.IsNotification) // the client is not interested in getting a response
			{
				return "";
			}

			JsonRpcResponse? response;
			if (procedureMetadata.MethodInfo.IsAsync())
			{
				if (!procedureMetadata.MethodInfo.ReturnType.IsGenericType)
				{
					await ((Task)result!).ConfigureAwait(false);
					response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id);
				}
				else
				{
					var ret = await ((dynamic)result!).ConfigureAwait(false);
					response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, ret);
				}
			}
			else
			{
				response = JsonRpcResponse.CreateResultResponse(jsonRpcRequest.Id, result);
			}
			return response.ToJson(_defaultSettings);
		}
		catch (TargetInvocationException e)
		{
			var ex = e.InnerException ?? e;
			return Error(JsonRpcErrorCodes.InternalError, ex.Message, jsonRpcRequest.Id);
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
			? response.ToJson(_defaultSettings)
			: "";
	}
}
