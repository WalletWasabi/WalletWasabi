using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Rpc;

/// <summary>
/// This class coordinates all the major steps in processing the RPC call.
/// It parses the json request, parses the parameters, invokes the service
/// methods and handles the errors.
/// </summary>
public class JsonRpcRequestHandler<TService>
{
	private static readonly JsonSerializerSettings DefaultSettings = new()
	{
		NullValueHandling = NullValueHandling.Ignore,
		ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
		Converters = new JsonConverter[]
		{
				new Uint256JsonConverter(),
				new OutPointAsTxoRefJsonConverter(),
				new BitcoinAddressJsonConverter()
		}
	};

	private static readonly JsonSerializer DefaultSerializer = JsonSerializer.Create(DefaultSettings);

	public JsonRpcRequestHandler(TService service)
	{
		Service = service;
		MetadataProvider = new JsonRpcServiceMetadataProvider(service.GetType());
	}

	private TService Service { get; }
	private JsonRpcServiceMetadataProvider MetadataProvider { get; }

	/// <summary>
	/// Parses the request and dispatches it to the correct service's method.
	/// </summary>
	/// <param name="body">The raw RPC request.</param>
	/// <param name="cancellationToken">The cancellation token that will be past to the service handler in case it expects/accepts one.</param>
	/// <returns>The response that, after serialization, is returned as response.</returns>
	public async Task<string> HandleAsync(string body, CancellationToken cancellationToken)
	{
		if (!JsonRpcRequest.TryParse(body, out var jsonRpcRequests, out var isBatch))
		{
			return JsonRpcResponse.CreateErrorResponse(null, JsonRpcErrorCodes.ParseError).ToJson(DefaultSettings);
		}
		var results = new List<string>();
		foreach (var jsonRpcRequest in jsonRpcRequests)
		{
			cancellationToken.ThrowIfCancellationRequested();
			results.Add(await HandleRequestAsync(jsonRpcRequest, cancellationToken).ConfigureAwait(false));
		}
		return isBatch ? $"[{string.Join(",", results)}]" : results[0];
	}

	private async Task<string> HandleRequestAsync(JsonRpcRequest jsonRpcRequest, CancellationToken cancellationToken)
	{
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
					var param = methodParameters[i];
					var item = jarr[i].ToObject(param.type, DefaultSerializer)
						?? throw new InvalidOperationException($"Parameter `{param.name}` is null.");
					parameters.Add(item);
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
			if (parameters.Count < methodParameters.Count(x => !x.isOptional))
			{
				return Error(JsonRpcErrorCodes.InvalidParams,
					$"{methodParameters.Count} parameters were expected but {parameters.Count} were received.", jsonRpcRequest.Id);
			}

			var missingParameters = methodParameters.Count - parameters.Count;
			parameters.AddRange(methodParameters.TakeLast(missingParameters).Select(x => x.defaultValue));
			var result = prodecureMetadata.MethodInfo.Invoke(Service, parameters.ToArray());

			if (jsonRpcRequest.IsNotification) // the client is not interested in getting a response
			{
				return "";
			}

			JsonRpcResponse? response = null;
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
			? response.ToJson(DefaultSettings)
			: "";
	}
}
