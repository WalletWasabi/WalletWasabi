using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Coordinator;

public delegate Task<Result<object, string>> JsonDecoder(Stream stream, Type modelType);

public class WasabiJsonInputFormatter : TextInputFormatter
{
	private readonly JsonDecoder _decoder;

	public WasabiJsonInputFormatter(JsonDecoder decoder)
	{
		_decoder = decoder;
		SupportedEncodings.Add(Encoding.UTF8);

		SupportedMediaTypes.Add("application/json");
	}

	public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
	{
		HttpContext httpContext = context.HttpContext;
		Stream requestStream = httpContext.Request.Body;
		try
		{
			var modelDeserializationResult = await _decoder(requestStream, context.ModelType).ConfigureAwait(false);
			if (!modelDeserializationResult.IsOk)
			{
				Logger.LogError(modelDeserializationResult.Error);
				return await InputFormatterResult.FailureAsync().ConfigureAwait(false);
			}

			var model = modelDeserializationResult.Value;
			return InputFormatterResult.Success(model);
		}
		catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
		{
		}
		catch (Exception e)
		{
			Logger.LogError(e);
		}

		return await InputFormatterResult.FailureAsync().ConfigureAwait(false);
	}
}
